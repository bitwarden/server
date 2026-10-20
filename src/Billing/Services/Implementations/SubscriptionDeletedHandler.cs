using Bit.Billing.Constants;
using Bit.Billing.Jobs;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Models.BitStripe;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Quartz;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;
using Event = Stripe.Event;
namespace Bit.Billing.Services.Implementations;

public class SubscriptionDeletedHandler : ISubscriptionDeletedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly ILogger<SubscriptionDeletedHandler> _logger;

    public SubscriptionDeletedHandler(
        IStripeEventService stripeEventService,
        IUserService userService,
        IUserRepository userRepository,
        IStripeEventUtilityService stripeEventUtilityService,
        IOrganizationDisableCommand organizationDisableCommand,
        IProviderRepository providerRepository,
        IProviderService providerService,
        ISchedulerFactory schedulerFactory,
        IPushNotificationAdapter pushNotificationAdapter,
        IStripeAdapter stripeAdapter,
        ILogger<SubscriptionDeletedHandler> logger)
    {
        _stripeEventService = stripeEventService;
        _userService = userService;
        _userRepository = userRepository;
        _stripeEventUtilityService = stripeEventUtilityService;
        _organizationDisableCommand = organizationDisableCommand;
        _providerRepository = providerRepository;
        _providerService = providerService;
        _schedulerFactory = schedulerFactory;
        _pushNotificationAdapter = pushNotificationAdapter;
        _stripeAdapter = stripeAdapter;
        _logger = logger;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.SubscriptionDeleted"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var subscription = await _stripeEventService.GetSubscription(parsedEvent, true);
        var (organizationId, userId, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);
        var subCanceled = subscription.Status == StripeSubscriptionStatus.Canceled;

        const string providerMigrationCancellationComment = "Cancelled as part of provider migration to Consolidated Billing";
        const string addedToProviderCancellationComment = "Organization was added to Provider";

        if (!subCanceled)
        {
            return;
        }

        // Run void before the disable branches so a disable failure doesn't strand
        // open invoices — on Stripe re-delivery the void is an idempotent no-op while
        // disable retries.
        if (CameFromUnpaidLifecycle(subscription))
        {
            await VoidOpenInvoicesAsync(subscription.Id, parsedEvent.Id);
        }

        if (organizationId.HasValue)
        {
            if (!string.IsNullOrEmpty(subscription.CancellationDetails?.Comment) &&
                (subscription.CancellationDetails.Comment == providerMigrationCancellationComment ||
                 subscription.CancellationDetails.Comment.Contains(addedToProviderCancellationComment)))
            {
                return;
            }

            await _organizationDisableCommand.DisableAsync(organizationId.Value, subscription.GetCurrentPeriodEnd());
        }
        else if (providerId.HasValue)
        {
            var provider = await _providerRepository.GetByIdAsync(providerId.Value);
            if (provider != null)
            {
                provider.Enabled = false;
                await _providerService.UpdateAsync(provider);

                await QueueProviderOrganizationDisableJobAsync(providerId.Value, subscription.GetCurrentPeriodEnd());
            }
        }
        else if (userId.HasValue)
        {
            await _userService.DisablePremiumAsync(userId.Value, subscription.GetCurrentPeriodEnd());
            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user != null)
            {
                await _pushNotificationAdapter.NotifyPremiumStatusChangedAsync(user!);
            }
        }
    }

    private static bool CameFromUnpaidLifecycle(Subscription subscription) =>
        subscription.Metadata != null &&
        subscription.Metadata.TryGetValue(MetadataKeys.CancellationOrigin, out var origin) &&
        origin == CancellationOrigins.UnpaidSubscription;

    private async Task VoidOpenInvoicesAsync(string subscriptionId, string eventId)
    {
        List<Invoice> openInvoices;
        try
        {
            openInvoices = await _stripeAdapter.ListInvoicesAsync(new StripeInvoiceListOptions
            {
                Status = InvoiceStatus.Open,
                Subscription = subscriptionId,
                SelectAll = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to list open invoices for canceled unpaid subscription ({SubscriptionID}) [event: {EventID}]; void cleanup skipped",
                subscriptionId, eventId);
            return;
        }

        foreach (var invoice in openInvoices)
        {
            await TryVoidInvoiceAsync(invoice.Id, subscriptionId, eventId);
        }

        _logger.LogInformation(
            "Void cleanup completed for canceled unpaid subscription ({SubscriptionID}) [event: {EventID}]: attempted {Count} open invoice(s)",
            subscriptionId, eventId, openInvoices.Count);
    }

    private async Task TryVoidInvoiceAsync(string invoiceId, string subscriptionId, string eventId)
    {
        try
        {
            await _stripeAdapter.VoidInvoiceAsync(invoiceId);
            _logger.LogInformation(
                "Voided invoice ({InvoiceID}) for canceled unpaid subscription ({SubscriptionID}) [event: {EventID}]",
                invoiceId, subscriptionId, eventId);
        }
        catch (StripeException ex)
        {
            // Likely cause: webhook re-delivery hitting an already-voided invoice. Continue
            // with remaining invoices rather than aborting the cleanup.
            _logger.LogWarning(ex,
                "Could not void invoice ({InvoiceID}) for canceled unpaid subscription ({SubscriptionID}) [event: {EventID}]; continuing with remaining invoices",
                invoiceId, subscriptionId, eventId);
        }
        catch (Exception ex)
        {
            // Catch transport-level failures (HttpRequestException, TaskCanceledException, etc.)
            // so the loop never abandons mid-page. Surface as Error since these are unexpected,
            // unlike the StripeException case above.
            _logger.LogError(ex,
                "Unexpected failure voiding invoice ({InvoiceID}) for canceled unpaid subscription ({SubscriptionID}) [event: {EventID}]; continuing with remaining invoices",
                invoiceId, subscriptionId, eventId);
        }
    }

    private async Task QueueProviderOrganizationDisableJobAsync(Guid providerId, DateTime? expirationDate)
    {
        var scheduler = await _schedulerFactory.GetScheduler();

        var job = JobBuilder.Create<ProviderOrganizationDisableJob>()
            .WithIdentity($"disable-provider-orgs-{providerId}", "provider-management")
            .UsingJobData("providerId", providerId.ToString())
            .UsingJobData("expirationDate", expirationDate?.ToString("O"))
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"disable-trigger-{providerId}", "provider-management")
            .StartNow()
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }
}
