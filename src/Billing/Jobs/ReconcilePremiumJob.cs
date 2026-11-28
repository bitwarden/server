using System.Runtime.CompilerServices;
using Bit.Billing.Services;
using Bit.Core;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bitwarden.Server.Sdk.Features;
using Quartz;
using Stripe;

namespace Bit.Billing.Jobs;

/// <summary>
/// Daily sweep over live Premium subscriptions that turns personal Premium back on for users paying for one, corrects
/// expirations that lag the user's current subscription, and reports users with more than one.
/// </summary>
/// <remarks>
/// Writes happen only while <see cref="FeatureFlagKeys.PM46168_ReconcilePremiumApply"/> is on; otherwise the sweep logs
/// what it would do.
/// </remarks>
[DisallowConcurrentExecution]
public class ReconcilePremiumJob(
    IStripeAdapter stripeAdapter,
    IPricingClient pricingClient,
    IUserRepository userRepository,
    IUserService userService,
    IPushNotificationAdapter pushNotificationAdapter,
    IFeatureService featureService,
    GlobalSettings globalSettings,
    ILogger<ReconcilePremiumJob> logger) : BaseJob(logger)
{
    private static readonly string[] _liveStatuses =
    [
        StripeConstants.SubscriptionStatus.Active,
        StripeConstants.SubscriptionStatus.Trialing,
        StripeConstants.SubscriptionStatus.PastDue
    ];

    private const int UserBatchSize = 1000;

    public static ITrigger GetTrigger() =>
        TriggerBuilder.Create()
            .WithIdentity("ReconcilePremiumTrigger")
            .StartNow()
            .WithCronSchedule("0 0 9 * * ?")
            .Build();

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM46168_ReconcilePremium))
        {
            return;
        }

        var run = new ReconciliationRun(featureService.IsEnabled(FeatureFlagKeys.PM46168_ReconcilePremiumApply));
        var subscriptionsByUser = await GetLiveSubscriptionsByUserAsync(context.CancellationToken);

        foreach (var batch in subscriptionsByUser.Chunk(UserBatchSize))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await ReconcileBatchAsync(batch, run);
        }

        _logger.LogInformation(
            "Premium reconciliation {Mode} complete. Users: {UserCount}, Enabled: {Enabled}, Enabled from unlinked: {EnabledUnlinked}, Expiration corrected: {ExpirationCorrected}, Duplicates: {Duplicates}, Missing users: {MissingUsers}, Errors: {Errors}",
            run.Mode, subscriptionsByUser.Count, run.Enabled, run.EnabledUnlinked, run.ExpirationCorrected, run.Duplicates,
            run.MissingUsers, run.Errors);
    }

    private async Task ReconcileBatchAsync(KeyValuePair<Guid, UserSubscriptions>[] batch, ReconciliationRun run)
    {
        var users = await TryGetUsersAsync(batch.Select(entry => entry.Key));

        if (users == null)
        {
            run.Errors += batch.Length;
            return;
        }

        foreach (var (userId, subscriptions) in batch)
        {
            await TryReconcileUserAsync(userId, users.GetValueOrDefault(userId), subscriptions, run);
        }
    }

    private async Task<Dictionary<Guid, User>?> TryGetUsersAsync(IEnumerable<Guid> userIds)
    {
        try
        {
            return (await userRepository.GetManyAsync(userIds)).ToDictionary(user => user.Id);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Premium reconciliation failed to read a batch of users");
            return null;
        }
    }

    private async Task TryReconcileUserAsync(Guid userId, User? user, UserSubscriptions subscriptions, ReconciliationRun run)
    {
        try
        {
            await ReconcileUserAsync(userId, user, subscriptions, run);
        }
        catch (Exception exception)
        {
            run.Errors++;
            _logger.LogError(exception, "Premium reconciliation failed for User ({UserId})", userId);
        }
    }

    private async Task ReconcileUserAsync(Guid userId, User? user, UserSubscriptions subscriptions, ReconciliationRun run)
    {
        if (user == null)
        {
            run.MissingUsers++;
            _logger.LogDebug(
                "Premium reconciliation: User ({UserId}) on live subscriptions ({SubscriptionIds}) does not exist",
                userId, subscriptions.Ids);
            return;
        }

        ReportDuplicates(user, subscriptions, run);

        if (!user.Premium)
        {
            await RestorePremiumAsync(user, subscriptions, run);
            return;
        }

        await CorrectExpirationAsync(user, subscriptions, run);
    }

    private void ReportDuplicates(User user, UserSubscriptions subscriptions, ReconciliationRun run)
    {
        if (!subscriptions.HasDuplicates)
        {
            return;
        }

        run.Duplicates++;
        _logger.LogWarning(
            "Premium reconciliation: User ({UserId}) has {SubscriptionCount} live Premium subscriptions ({SubscriptionIds}); current is ({CurrentSubscriptionId})",
            user.Id, subscriptions.Count, subscriptions.Ids, user.GatewaySubscriptionId);
    }

    private async Task RestorePremiumAsync(User user, UserSubscriptions subscriptions, ReconciliationRun run)
    {
        var linked = subscriptions.LinkedTo(user);
        var source = linked ?? subscriptions.Latest;

        _logger.LogInformation(
            "Premium reconciliation: {Action} Premium for User ({UserId}) from {Link} subscription ({SubscriptionId})",
            run.Apply ? "Enabling" : "Would enable", user.Id, linked != null ? "current" : "unlinked", source.Id);

        if (run.Apply)
        {
            await EnablePremiumAsync(user, source.CurrentPeriodEnd);
        }

        if (linked != null)
        {
            run.Enabled++;
        }
        else
        {
            run.EnabledUnlinked++;
        }
    }

    private async Task CorrectExpirationAsync(User user, UserSubscriptions subscriptions, ReconciliationRun run)
    {
        var linkedPeriodEnd = subscriptions.LinkedTo(user)?.CurrentPeriodEnd;

        if (!ExpirationLags(user, linkedPeriodEnd))
        {
            return;
        }

        _logger.LogInformation(
            "Premium reconciliation: {Action} Premium expiration for User ({UserId}) from {CurrentExpiration} to {LinkedPeriodEnd}",
            run.Apply ? "Correcting" : "Would correct", user.Id, user.PremiumExpirationDate, linkedPeriodEnd);

        if (run.Apply)
        {
            await userService.UpdatePremiumExpirationAsync(user.Id, linkedPeriodEnd);
        }

        run.ExpirationCorrected++;
    }

    private static bool ExpirationLags(User user, DateTime? linkedPeriodEnd) =>
        linkedPeriodEnd.HasValue && (!user.PremiumExpirationDate.HasValue || user.PremiumExpirationDate < linkedPeriodEnd);

    private async Task EnablePremiumAsync(User user, DateTime? expirationDate)
    {
        await userService.EnablePremiumAsync(user.Id, expirationDate);

        var updated = await userRepository.GetByIdAsync(user.Id);

        if (updated is not { Premium: true })
        {
            _logger.LogWarning("Premium reconciliation: Premium for User ({UserId}) was not enabled; the user has no gateway", user.Id);
            return;
        }

        await pushNotificationAdapter.NotifyPremiumStatusChangedAsync(updated);
    }

    private async Task<Dictionary<Guid, UserSubscriptions>> GetLiveSubscriptionsByUserAsync(CancellationToken cancellationToken)
    {
        var subscriptionsByUser = new Dictionary<Guid, UserSubscriptions>();
        var seen = new HashSet<string>();

        await foreach (var subscription in ListLivePremiumSubscriptionsAsync(cancellationToken))
        {
            if (!seen.Add(subscription.Id) || !IsInRegion(subscription) || !TryGetUserId(subscription, out var userId))
            {
                continue;
            }

            if (!subscriptionsByUser.TryGetValue(userId, out var subscriptions))
            {
                subscriptions = new UserSubscriptions();
                subscriptionsByUser[userId] = subscriptions;
            }

            subscriptions.Add(new LiveSubscription(subscription.Id, subscription.GetCurrentPeriodEnd()));
        }

        return subscriptionsByUser;
    }

    private async IAsyncEnumerable<Subscription> ListLivePremiumSubscriptionsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var priceIds = premiumPlans.Select(plan => plan.Seat.StripePriceId).Distinct();

        foreach (var (priceId, status) in priceIds.SelectMany(_ => _liveStatuses, (priceId, status) => (priceId, status)))
        {
            await foreach (var subscription in ListSubscriptionsAsync(priceId, status, cancellationToken))
            {
                yield return subscription;
            }
        }
    }

    private async IAsyncEnumerable<Subscription> ListSubscriptionsAsync(
        string priceId,
        string status,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? startingAfter = null;
        StripeList<Subscription> page;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            page = await stripeAdapter.ListSubscriptionsAsync(new SubscriptionListOptions
            {
                Price = priceId,
                Status = status,
                Limit = 100,
                StartingAfter = startingAfter,
                Expand = ["data.customer"]
            });

            foreach (var subscription in page.Data)
            {
                yield return subscription;
            }

            startingAfter = page.Data.LastOrDefault()?.Id;
        } while (page.HasMore && startingAfter != null);
    }

    private bool IsInRegion(Subscription subscription) =>
        subscription.Customer?.GetRegion() == globalSettings.BaseServiceUri.CloudRegion;

    private static bool TryGetUserId(Subscription subscription, out Guid userId)
    {
        userId = Guid.Empty;

        return subscription.Metadata != null &&
               subscription.Metadata.TryGetValue(StripeConstants.MetadataKeys.UserId, out var value) &&
               Guid.TryParse(value, out userId);
    }

    private record LiveSubscription(string Id, DateTime? CurrentPeriodEnd);

    private sealed class UserSubscriptions
    {
        private readonly List<LiveSubscription> _subscriptions = [];

        public int Count => _subscriptions.Count;

        public bool HasDuplicates => _subscriptions.Count > 1;

        public string Ids => string.Join(", ", _subscriptions.Select(subscription => subscription.Id));

        public LiveSubscription Latest => _subscriptions.OrderByDescending(subscription => subscription.CurrentPeriodEnd).First();

        public void Add(LiveSubscription subscription) => _subscriptions.Add(subscription);

        public LiveSubscription? LinkedTo(User user) =>
            _subscriptions.FirstOrDefault(subscription => subscription.Id == user.GatewaySubscriptionId);
    }

    private sealed class ReconciliationRun(bool apply)
    {
        public bool Apply { get; } = apply;

        public string Mode => Apply ? "run" : "dry run";

        public int Enabled { get; set; }

        public int EnabledUnlinked { get; set; }

        public int ExpirationCorrected { get; set; }

        public int Duplicates { get; set; }

        public int MissingUsers { get; set; }

        public int Errors { get; set; }
    }
}
