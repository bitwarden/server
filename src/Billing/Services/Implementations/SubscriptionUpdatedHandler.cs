using Bit.Billing.Constants;
using Bit.Billing.Jobs;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Quartz;
using Stripe;
using Stripe.TestHelpers;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class SubscriptionUpdatedHandler : ISubscriptionUpdatedHandler
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationService _organizationService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IOrganizationSponsorshipRenewCommand _organizationSponsorshipRenewCommand;
    private readonly IUserService _userService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IOrganizationEnableCommand _organizationEnableCommand;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IPricingClient _pricingClient;
    private readonly IFeatureService _featureService;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly ILogger<SubscriptionUpdatedHandler> _logger;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;

    public SubscriptionUpdatedHandler(
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService,
        IOrganizationService organizationService,
        IStripeFacade stripeFacade,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IUserService userService,
        IOrganizationRepository organizationRepository,
        ISchedulerFactory schedulerFactory,
        IOrganizationEnableCommand organizationEnableCommand,
        IOrganizationDisableCommand organizationDisableCommand,
        IPricingClient pricingClient,
        IFeatureService featureService,
        IProviderRepository providerRepository,
        IProviderService providerService,
        ILogger<SubscriptionUpdatedHandler> logger,
        IPushNotificationAdapter pushNotificationAdapter)
    {
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _organizationService = organizationService;
        _providerService = providerService;
        _stripeFacade = stripeFacade;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _userService = userService;
        _organizationRepository = organizationRepository;
        _providerRepository = providerRepository;
        _schedulerFactory = schedulerFactory;
        _organizationEnableCommand = organizationEnableCommand;
        _organizationDisableCommand = organizationDisableCommand;
        _pricingClient = pricingClient;
        _featureService = featureService;
        _providerRepository = providerRepository;
        _providerService = providerService;
        _logger = logger;
        _pushNotificationAdapter = pushNotificationAdapter;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.SubscriptionUpdated"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var subscription = await _stripeEventService.GetSubscription(parsedEvent, true, ["customer", "discounts", "latest_invoice", "test_clock"]);
        var (organizationId, userId, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        var currentPeriodEnd = subscription.GetCurrentPeriodEnd();

        switch (subscription.Status)
        {
            case StripeSubscriptionStatus.Unpaid or StripeSubscriptionStatus.IncompleteExpired
                when organizationId.HasValue:
                {
                    await _organizationDisableCommand.DisableAsync(organizationId.Value, currentPeriodEnd);
                    if (subscription.Status == StripeSubscriptionStatus.Unpaid &&
                        subscription.LatestInvoice is { BillingReason: "subscription_cycle" or "subscription_create" })
                    {
                        await ScheduleCancellationJobAsync(subscription.Id, organizationId.Value);
                    }
                    break;
                }
            case StripeSubscriptionStatus.Unpaid or StripeSubscriptionStatus.IncompleteExpired when providerId.HasValue:
                {
                    await HandleUnpaidProviderSubscriptionAsync(providerId.Value, parsedEvent, subscription);
                    break;
                }
            case StripeSubscriptionStatus.Unpaid or StripeSubscriptionStatus.IncompleteExpired:
                {
                    if (!userId.HasValue)
                    {
                        break;
                    }

                    if (await IsPremiumSubscriptionAsync(subscription))
                    {
                        await CancelSubscription(subscription.Id);
                        await VoidOpenInvoices(subscription.Id);
                    }

                    // Only disable premium if the user doesn't have other active premium subscriptions
                    // This prevents incorrectly disabling premium when webhook events arrive out of order
                    if (!await HasOtherActivePremiumSubscriptionsAsync(userId.Value, subscription.Id))
                    {
                        await _userService.DisablePremiumAsync(userId.Value, currentPeriodEnd);
                    }

                    break;
                }
            case StripeSubscriptionStatus.Incomplete when userId.HasValue:
                {
                    // Handle Incomplete subscriptions for Premium users that have open invoices from failed payments
                    // This prevents duplicate subscriptions when users retry the subscription flow
                    if (await IsPremiumSubscriptionAsync(subscription) &&
                        subscription.LatestInvoice is { Status: StripeInvoiceStatus.Open })
                    {
                        await CancelSubscription(subscription.Id);
                        await VoidOpenInvoices(subscription.Id);

                        // Only disable premium if the user doesn't have other active premium subscriptions
                        // This prevents incorrectly disabling premium when webhook events arrive out of order
                        if (!await HasOtherActivePremiumSubscriptionsAsync(userId.Value, subscription.Id))
                        {
                            await _userService.DisablePremiumAsync(userId.Value, currentPeriodEnd);
                        }
                    }

                    break;
                }
            case StripeSubscriptionStatus.Active when organizationId.HasValue:
                {
                    await _organizationEnableCommand.EnableAsync(organizationId.Value);
                    var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);
                    if (organization != null)
                    {
                        await _pushNotificationAdapter.NotifyEnabledChangedAsync(organization);
                    }
                    break;
                }
            case StripeSubscriptionStatus.Active when providerId.HasValue:
                {
                    var provider = await _providerRepository.GetByIdAsync(providerId.Value);
                    if (provider != null)
                    {
                        provider.Enabled = true;
                        await _providerService.UpdateAsync(provider);

                        if (IsProviderSubscriptionNowActive(parsedEvent, subscription))
                        {
                            // Update the CancelAtPeriodEnd subscription option to prevent the now active provider subscription from being cancelled
                            var subscriptionUpdateOptions = new SubscriptionUpdateOptions { CancelAtPeriodEnd = false };
                            await _stripeFacade.UpdateSubscription(subscription.Id, subscriptionUpdateOptions);
                        }
                    }
                    break;
                }
            case StripeSubscriptionStatus.Active:
                {
                    if (userId.HasValue)
                    {
                        await _userService.EnablePremiumAsync(userId.Value, currentPeriodEnd);
                    }
                    break;
                }
        }

        if (organizationId.HasValue)
        {
            await _organizationService.UpdateExpirationDateAsync(organizationId.Value, currentPeriodEnd);
            if (_stripeEventUtilityService.IsSponsoredSubscription(subscription) && currentPeriodEnd.HasValue)
            {
                await _organizationSponsorshipRenewCommand.UpdateExpirationDateAsync(organizationId.Value, currentPeriodEnd.Value);
            }

            await RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(parsedEvent, subscription);
        }
        else if (userId.HasValue)
        {
            await _userService.UpdatePremiumExpirationAsync(userId.Value, currentPeriodEnd);
        }
    }

    private async Task CancelSubscription(string subscriptionId) =>
        await _stripeFacade.CancelSubscription(subscriptionId, new SubscriptionCancelOptions());

    private async Task VoidOpenInvoices(string subscriptionId)
    {
        var options = new InvoiceListOptions
        {
            Status = StripeInvoiceStatus.Open,
            Subscription = subscriptionId
        };
        var invoices = await _stripeFacade.ListInvoices(options);
        foreach (var invoice in invoices)
        {
            await _stripeFacade.VoidInvoice(invoice.Id);
        }
    }

    private async Task<bool> IsPremiumSubscriptionAsync(Subscription subscription)
    {
        var premiumPlans = await _pricingClient.ListPremiumPlans();
        var premiumPriceIds = premiumPlans.SelectMany(p => new[] { p.Seat.StripePriceId, p.Storage.StripePriceId }).ToHashSet();

        // Add hardcoded fallbacks for safety to ensure all premium plan variants are covered
        premiumPriceIds.Add(IStripeEventUtilityService.PremiumPlanId);
        premiumPriceIds.Add(IStripeEventUtilityService.PremiumPlanIdAppStore);

        return subscription.Items.Any(i => premiumPriceIds.Contains(i.Price.Id));
    }

    /// <summary>
    /// Checks if a user has other active premium subscriptions besides the current one being processed.
    /// This prevents incorrectly disabling premium when a user has multiple subscriptions due to payment retry scenarios.
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    /// <param name="currentSubscriptionId">The current subscription being processed (to exclude from the check)</param>
    /// <returns>True if the user has other active premium subscriptions, false otherwise</returns>
    private async Task<bool> HasOtherActivePremiumSubscriptionsAsync(Guid userId, string currentSubscriptionId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.Email))
        {
            return false;
        }

        // Find the customer by email
        var customerOptions = new CustomerListOptions
        {
            Email = user.Email,
            Limit = 1
        };
        var customers = await _stripeFacade.ListCustomers(customerOptions);
        var customer = customers.FirstOrDefault();

        if (customer == null)
        {
            return false;
        }

        // List all active subscriptions for this customer
        var subscriptionOptions = new SubscriptionListOptions
        {
            Customer = customer.Id,
            Status = "active"
        };

        var activeSubscriptions = await _stripeFacade.ListSubscriptions(subscriptionOptions);

        // Check if any other subscription (not the current one) is a premium subscription
        foreach (var sub in activeSubscriptions.Where(s => s.Id != currentSubscriptionId))
        {
            if (await IsPremiumSubscriptionAsync(sub))
            {
                _logger.LogInformation(
                    "User {UserId} has another active premium subscription {SubscriptionId} while processing {CurrentSubscriptionId}",
                    userId, sub.Id, currentSubscriptionId);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the provider subscription status has changed from a non-active to an active status type
    /// If the previous status is already active(active,past-due,trialing),canceled,or null, then this will return false.
    /// </summary>
    /// <param name="parsedEvent">The event containing the previous subscription status</param>
    /// <param name="subscription">The current subscription status</param>
    /// <returns>A boolean that represents whether the event status has changed from a non-active status to an active status</returns>
    private static bool IsProviderSubscriptionNowActive(Event parsedEvent, Subscription subscription)
    {
        if (parsedEvent.Data.PreviousAttributes == null)
        {
            return false;
        }

        var previousSubscription = parsedEvent
            .Data
            .PreviousAttributes
            .ToObject<Subscription>() as Subscription;

        return previousSubscription?.Status switch
        {
            StripeSubscriptionStatus.IncompleteExpired
                or StripeSubscriptionStatus.Paused
                or StripeSubscriptionStatus.Incomplete
                or StripeSubscriptionStatus.Unpaid
                when subscription.Status == StripeSubscriptionStatus.Active => true,
            _ => false
        };
    }

    /// <summary>
    /// Removes the Password Manager coupon if the organization is removing the Secrets Manager trial.
    /// Only applies to organizations that have a subscription from the Secrets Manager trial.
    /// </summary>
    /// <param name="parsedEvent"></param>
    /// <param name="subscription"></param>
    private async Task RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(
        Event parsedEvent,
        Subscription subscription)
    {
        if (parsedEvent.Data.PreviousAttributes?.items is null)
        {
            return;
        }

        var organization = subscription.Metadata.TryGetValue("organizationId", out var organizationId)
            ? await _organizationRepository.GetByIdAsync(Guid.Parse(organizationId))
            : null;

        if (organization == null)
        {
            return;
        }

        var plan = await _pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsSecretsManager)
        {
            return;
        }

        var previousSubscription = parsedEvent.Data
            .PreviousAttributes
            .ToObject<Subscription>() as Subscription;

        // This being false doesn't necessarily mean that the organization doesn't subscribe to Secrets Manager.
        // If there are changes to any subscription item, Stripe sends every item in the subscription, both
        // changed and unchanged.
        var previousSubscriptionHasSecretsManager =
            previousSubscription?.Items is not null &&
            previousSubscription.Items.Any(
                previousSubscriptionItem => previousSubscriptionItem.Plan.Id == plan.SecretsManager.StripeSeatPlanId);

        var currentSubscriptionHasSecretsManager =
            subscription.Items.Any(
                currentSubscriptionItem => currentSubscriptionItem.Plan.Id == plan.SecretsManager.StripeSeatPlanId);

        if (!previousSubscriptionHasSecretsManager || currentSubscriptionHasSecretsManager)
        {
            return;
        }

        var customerHasSecretsManagerTrial = subscription.Customer
            ?.Discount
            ?.Coupon
            ?.Id == "sm-standalone";

        var subscriptionHasSecretsManagerTrial = subscription.Discounts.Select(discount => discount.Coupon.Id)
            .Contains(StripeConstants.CouponIDs.SecretsManagerStandalone);

        if (customerHasSecretsManagerTrial)
        {
            await _stripeFacade.DeleteCustomerDiscount(subscription.CustomerId);
        }

        if (subscriptionHasSecretsManagerTrial)
        {
            await _stripeFacade.DeleteSubscriptionDiscount(subscription.Id);
        }
    }

    private async Task ScheduleCancellationJobAsync(string subscriptionId, Guid organizationId)
    {
        var scheduler = await _schedulerFactory.GetScheduler();

        var job = JobBuilder.Create<SubscriptionCancellationJob>()
            .WithIdentity($"cancel-sub-{subscriptionId}", "subscription-cancellations")
            .UsingJobData("subscriptionId", subscriptionId)
            .UsingJobData("organizationId", organizationId.ToString())
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"cancel-trigger-{subscriptionId}", "subscription-cancellations")
            .StartAt(DateTimeOffset.UtcNow.AddDays(7))
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }

    private async Task HandleUnpaidProviderSubscriptionAsync(
        Guid providerId,
        Event parsedEvent,
        Subscription currentSubscription)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider == null)
        {
            return;
        }

        try
        {
            provider.Enabled = false;
            await _providerService.UpdateAsync(provider);

            if (parsedEvent.Data.PreviousAttributes != null)
            {
                var previousSubscription = parsedEvent.Data.PreviousAttributes.ToObject<Subscription>() as Subscription;

                if (previousSubscription is
                    {
                        Status:
                        StripeSubscriptionStatus.Trialing or
                        StripeSubscriptionStatus.Active or
                        StripeSubscriptionStatus.PastDue
                    } && currentSubscription is
                    {
                        Status: StripeSubscriptionStatus.Unpaid,
                        LatestInvoice.BillingReason: "subscription_cycle" or "subscription_create"
                    })
                {
                    if (currentSubscription.TestClock != null)
                    {
                        await WaitForTestClockToAdvanceAsync(currentSubscription.TestClock);
                    }

                    var now = currentSubscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

                    var subscriptionUpdateOptions = new SubscriptionUpdateOptions { CancelAt = now.AddDays(7) };

                    await _stripeFacade.UpdateSubscription(currentSubscription.Id, subscriptionUpdateOptions);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occurred while trying to disable and schedule subscription cancellation for provider ({ProviderID})", providerId);
        }
    }

    private async Task WaitForTestClockToAdvanceAsync(TestClock testClock)
    {
        while (testClock.Status != "ready")
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            testClock = await _stripeFacade.GetTestClock(testClock.Id);
            if (testClock.Status == "internal_failure")
            {
                throw new Exception("Stripe Test Clock encountered an internal failure");
            }
        }
    }
}
