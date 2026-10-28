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
    ILogger<ReconcilePremiumJob> logger) : BaseJob(logger)
{
    private static readonly string[] _liveStatuses =
    [
        StripeConstants.SubscriptionStatus.Active,
        StripeConstants.SubscriptionStatus.Trialing,
        StripeConstants.SubscriptionStatus.PastDue
    ];

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

        var apply = featureService.IsEnabled(FeatureFlagKeys.PM46168_ReconcilePremiumApply);

        var subscriptionsByUser = await GetLiveSubscriptionsByUserAsync(context.CancellationToken);

        int enabled = 0, enabledUnlinked = 0, expirationCorrected = 0, duplicates = 0, missingUsers = 0, errors = 0;

        foreach (var (userId, subscriptions) in subscriptionsByUser)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                var user = await userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    missingUsers++;
                    _logger.LogDebug(
                        "Premium reconciliation: User ({UserId}) on live subscriptions ({SubscriptionIds}) does not exist",
                        userId, string.Join(", ", subscriptions.Select(subscription => subscription.Id)));
                    continue;
                }

                if (subscriptions.Count > 1)
                {
                    duplicates++;
                    _logger.LogWarning(
                        "Premium reconciliation: User ({UserId}) has {SubscriptionCount} live Premium subscriptions ({SubscriptionIds}); current is ({CurrentSubscriptionId})",
                        user.Id, subscriptions.Count, string.Join(", ", subscriptions.Select(subscription => subscription.Id)),
                        user.GatewaySubscriptionId);
                }

                var linked = subscriptions.FirstOrDefault(subscription => subscription.Id == user.GatewaySubscriptionId);

                if (!user.Premium)
                {
                    var source = linked ?? subscriptions.OrderByDescending(subscription => subscription.CurrentPeriodEnd).First();

                    _logger.LogInformation(
                        "Premium reconciliation: {Action} Premium for User ({UserId}) from {Link} subscription ({SubscriptionId})",
                        apply ? "Enabling" : "Would enable", user.Id, linked != null ? "current" : "unlinked", source.Id);

                    if (apply)
                    {
                        await EnablePremiumAsync(user, source.CurrentPeriodEnd);
                    }

                    if (linked != null)
                    {
                        enabled++;
                    }
                    else
                    {
                        enabledUnlinked++;
                    }

                    continue;
                }

                var linkedPeriodEnd = linked?.CurrentPeriodEnd;

                if (linkedPeriodEnd.HasValue && (!user.PremiumExpirationDate.HasValue || user.PremiumExpirationDate < linkedPeriodEnd))
                {
                    _logger.LogInformation(
                        "Premium reconciliation: {Action} Premium expiration for User ({UserId}) from {CurrentExpiration} to {LinkedPeriodEnd}",
                        apply ? "Correcting" : "Would correct", user.Id, user.PremiumExpirationDate, linkedPeriodEnd);

                    if (apply)
                    {
                        await userService.UpdatePremiumExpirationAsync(user.Id, linkedPeriodEnd);
                    }

                    expirationCorrected++;
                }
            }
            catch (Exception exception)
            {
                errors++;
                _logger.LogError(exception, "Premium reconciliation failed for User ({UserId})", userId);
            }
        }

        _logger.LogInformation(
            "Premium reconciliation {Mode} complete. Users: {UserCount}, Enabled: {Enabled}, Enabled from unlinked: {EnabledUnlinked}, Expiration corrected: {ExpirationCorrected}, Duplicates: {Duplicates}, Missing users: {MissingUsers}, Errors: {Errors}",
            apply ? "run" : "dry run", subscriptionsByUser.Count, enabled, enabledUnlinked, expirationCorrected, duplicates, missingUsers, errors);
    }

    private async Task<Dictionary<Guid, List<LiveSubscription>>> GetLiveSubscriptionsByUserAsync(CancellationToken cancellationToken)
    {
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var subscriptionsByUser = new Dictionary<Guid, List<LiveSubscription>>();
        var seen = new HashSet<string>();

        foreach (var priceId in premiumPlans.Select(plan => plan.Seat.StripePriceId).Distinct())
        {
            foreach (var status in _liveStatuses)
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
                        StartingAfter = startingAfter
                    });

                    foreach (var subscription in page.Data)
                    {
                        if (!seen.Add(subscription.Id) ||
                            subscription.Metadata == null ||
                            !subscription.Metadata.TryGetValue(StripeConstants.MetadataKeys.UserId, out var value) ||
                            !Guid.TryParse(value, out var userId))
                        {
                            continue;
                        }

                        if (!subscriptionsByUser.TryGetValue(userId, out var subscriptions))
                        {
                            subscriptions = [];
                            subscriptionsByUser[userId] = subscriptions;
                        }

                        subscriptions.Add(new LiveSubscription(subscription.Id, subscription.GetCurrentPeriodEnd()));
                    }

                    startingAfter = page.Data.LastOrDefault()?.Id;
                } while (page.HasMore && startingAfter != null);
            }
        }

        return subscriptionsByUser;
    }

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

    private record LiveSubscription(string Id, DateTime? CurrentPeriodEnd);
}
