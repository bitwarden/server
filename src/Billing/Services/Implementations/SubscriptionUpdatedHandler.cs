﻿using Bit.Billing.Constants;
using Bit.Billing.Jobs;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Pricing;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Quartz;
using Stripe;
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
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationEnableCommand _organizationEnableCommand;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IPricingClient _pricingClient;

    public SubscriptionUpdatedHandler(
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService,
        IOrganizationService organizationService,
        IStripeFacade stripeFacade,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IUserService userService,
        IPushNotificationService pushNotificationService,
        IOrganizationRepository organizationRepository,
        ISchedulerFactory schedulerFactory,
        IFeatureService featureService,
        IOrganizationEnableCommand organizationEnableCommand,
        IOrganizationDisableCommand organizationDisableCommand,
        IPricingClient pricingClient)
    {
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _organizationService = organizationService;
        _stripeFacade = stripeFacade;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _userService = userService;
        _pushNotificationService = pushNotificationService;
        _organizationRepository = organizationRepository;
        _schedulerFactory = schedulerFactory;
        _featureService = featureService;
        _organizationEnableCommand = organizationEnableCommand;
        _organizationDisableCommand = organizationDisableCommand;
        _pricingClient = pricingClient;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.SubscriptionUpdated"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var subscription = await _stripeEventService.GetSubscription(parsedEvent, true, ["customer", "discounts", "latest_invoice"]);
        var (organizationId, userId, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        switch (subscription.Status)
        {
            case StripeSubscriptionStatus.Unpaid or StripeSubscriptionStatus.IncompleteExpired
                when organizationId.HasValue:
                {
                    await _organizationDisableCommand.DisableAsync(organizationId.Value, subscription.CurrentPeriodEnd);
                    if (subscription.Status == StripeSubscriptionStatus.Unpaid &&
                        subscription.LatestInvoice is { BillingReason: "subscription_cycle" or "subscription_create" })
                    {
                        await ScheduleCancellationJobAsync(subscription.Id, organizationId.Value);
                    }
                    break;
                }
            case StripeSubscriptionStatus.Unpaid or StripeSubscriptionStatus.IncompleteExpired:
                {
                    if (!userId.HasValue)
                    {
                        break;
                    }

                    if (subscription.Status is StripeSubscriptionStatus.Unpaid &&
                        subscription.Items.Any(i => i.Price.Id is IStripeEventUtilityService.PremiumPlanId or IStripeEventUtilityService.PremiumPlanIdAppStore))
                    {
                        await CancelSubscription(subscription.Id);
                        await VoidOpenInvoices(subscription.Id);
                    }

                    await _userService.DisablePremiumAsync(userId.Value, subscription.CurrentPeriodEnd);

                    break;
                }
            case StripeSubscriptionStatus.Active when organizationId.HasValue:
                {
                    await _organizationEnableCommand.EnableAsync(organizationId.Value);
                    var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);
                    if (organization != null)
                    {
                        await _pushNotificationService.PushSyncOrganizationStatusAsync(organization);
                    }
                    break;
                }
            case StripeSubscriptionStatus.Active:
                {
                    if (userId.HasValue)
                    {
                        await _userService.EnablePremiumAsync(userId.Value, subscription.CurrentPeriodEnd);
                    }

                    break;
                }
        }

        if (organizationId.HasValue)
        {
            await _organizationService.UpdateExpirationDateAsync(organizationId.Value, subscription.CurrentPeriodEnd);
            if (_stripeEventUtilityService.IsSponsoredSubscription(subscription))
            {
                await _organizationSponsorshipRenewCommand.UpdateExpirationDateAsync(organizationId.Value, subscription.CurrentPeriodEnd);
            }

            await RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(parsedEvent, subscription);
        }
        else if (userId.HasValue)
        {
            await _userService.UpdatePremiumExpirationAsync(userId.Value, subscription.CurrentPeriodEnd);
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

        var subscriptionHasSecretsManagerTrial = subscription.Discount
            ?.Coupon
            ?.Id == "sm-standalone";

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
        var isResellerManagedOrgAlertEnabled = _featureService.IsEnabled(FeatureFlagKeys.ResellerManagedOrgAlert);
        if (!isResellerManagedOrgAlertEnabled)
        {
            return;
        }

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
}
