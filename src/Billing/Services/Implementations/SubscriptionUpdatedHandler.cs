using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;
using Stripe.TestHelpers;
using static Bit.Core.Billing.Constants.StripeConstants;
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
    private readonly IPricingClient _pricingClient;
    private readonly ISubscriberService _subscriberService;
    private readonly IOrganizationRepository _organizationRepository;

    public SubscriptionUpdatedHandler(
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService,
        IOrganizationService organizationService,
        IStripeFacade stripeFacade,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IUserService userService,
        IPricingClient pricingClient,
        ISubscriberService subscriberService,
        IOrganizationRepository organizationRepository)
    {
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _organizationService = organizationService;
        _stripeFacade = stripeFacade;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _userService = userService;
        _pricingClient = pricingClient;
        _subscriberService = subscriberService;
        _organizationRepository = organizationRepository;
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        var subscription = await _stripeEventService.GetSubscription(parsedEvent, true, ["customer", "discounts", "latest_invoice", "test_clock"]);
        SubscriberId subscriberId = subscription;

        var currentPeriodEnd = subscription.GetCurrentPeriodEnd();

        if (SubscriptionWentUnpaid(parsedEvent, subscription))
        {
            await _subscriberService.DisableSubscriberAsync(subscriberId, currentPeriodEnd);
            await SetSubscriptionToCancelAsync(subscription);
        }
        else if (SubscriptionBecameActive(parsedEvent, subscription))
        {
            await _subscriberService.EnableSubscriberAsync(subscriberId, currentPeriodEnd);
            await RemovePendingCancellationAsync(subscription);
        }

        await subscriberId.Match(
            userId => _userService.UpdatePremiumExpirationAsync(userId.Value, currentPeriodEnd),
            async organizationId =>
            {
                await _organizationService.UpdateExpirationDateAsync(organizationId.Value, currentPeriodEnd);

                if (_stripeEventUtilityService.IsSponsoredSubscription(subscription) && currentPeriodEnd.HasValue)
                {
                    await _organizationSponsorshipRenewCommand.UpdateExpirationDateAsync(organizationId.Value, currentPeriodEnd.Value);
                }

                await RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(parsedEvent, subscription);
            },
            _ => Task.CompletedTask);
    }

    private static bool SubscriptionWentUnpaid(
        Event parsedEvent,
        Subscription currentSubscription) =>
        parsedEvent.Data.PreviousAttributes.ToObject<Subscription>() is Subscription
        {
            Status:
            SubscriptionStatus.Trialing or
            SubscriptionStatus.Active or
            SubscriptionStatus.PastDue
        } && currentSubscription is
        {
            Status: SubscriptionStatus.Unpaid,
            LatestInvoice.BillingReason: BillingReasons.SubscriptionCreate or BillingReasons.SubscriptionCycle
        };

    private static bool SubscriptionBecameActive(
        Event parsedEvent,
        Subscription currentSubscription) =>
        parsedEvent.Data.PreviousAttributes.ToObject<Subscription>() is Subscription
        {
            Status:
            SubscriptionStatus.Incomplete or
            SubscriptionStatus.Unpaid
        } && currentSubscription is
        {
            Status: SubscriptionStatus.Active,
            LatestInvoice.BillingReason: BillingReasons.SubscriptionCreate or BillingReasons.SubscriptionCycle
        };

    private async Task SetSubscriptionToCancelAsync(Subscription subscription)
    {
        if (subscription.TestClock != null)
        {
            await WaitForTestClockToAdvanceAsync(subscription.TestClock);
        }

        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

        await _stripeFacade.UpdateSubscription(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAt = now.AddDays(7),
            ProrationBehavior = ProrationBehavior.None,
            CancellationDetails = new SubscriptionCancellationDetailsOptions
            {
                Comment = $"Automation: Setting unpaid subscription to cancel 7 days from {now:yyyy-MM-dd}."
            }
        });
    }

    private async Task RemovePendingCancellationAsync(Subscription subscription)
        => await _stripeFacade.UpdateSubscription(subscription.Id, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false,
            ProrationBehavior = ProrationBehavior.None
        });

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

        // Get all plan IDs that include Secrets Manager support to check if the organization has secret manager in the
        // previous and/or current subscriptions.
        var planIdsOfPlansWithSecretManager = (await _pricingClient.ListPlans())
            .Where(orgPlan => orgPlan.SupportsSecretsManager && orgPlan.SecretsManager.StripeSeatPlanId != null)
            .Select(orgPlan => orgPlan.SecretsManager.StripeSeatPlanId)
            .ToHashSet();

        // This being false doesn't necessarily mean that the organization doesn't subscribe to Secrets Manager.
        // If there are changes to any subscription item, Stripe sends every item in the subscription, both
        // changed and unchanged.
        var previousSubscriptionHasSecretsManager =
            previousSubscription?.Items is not null &&
            previousSubscription.Items.Any(
                previousSubscriptionItem => planIdsOfPlansWithSecretManager.Contains(previousSubscriptionItem.Plan.Id));

        var currentSubscriptionHasSecretsManager =
            subscription.Items.Any(
                currentSubscriptionItem => planIdsOfPlansWithSecretManager.Contains(currentSubscriptionItem.Plan.Id));

        if (!previousSubscriptionHasSecretsManager || currentSubscriptionHasSecretsManager)
        {
            return;
        }

        var customerHasSecretsManagerTrial = subscription.Customer
            ?.Discount
            ?.Coupon
            ?.Id == "sm-standalone";

        var subscriptionHasSecretsManagerTrial = subscription.Discounts.Select(discount => discount.Coupon.Id)
            .Contains(CouponIDs.SecretsManagerStandalone);

        if (customerHasSecretsManagerTrial)
        {
            await _stripeFacade.DeleteCustomerDiscount(subscription.CustomerId);
        }

        if (subscriptionHasSecretsManagerTrial)
        {
            await _stripeFacade.DeleteSubscriptionDiscount(subscription.Id);
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
