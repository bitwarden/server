using Bit.Billing.Constants;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Services;
using Bit.Core.Utilities;
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

    public SubscriptionUpdatedHandler(
        IStripeEventService stripeEventService,
        IStripeEventUtilityService stripeEventUtilityService,
        IOrganizationService organizationService,
        IStripeFacade stripeFacade,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IUserService userService)
    {
        _stripeEventService = stripeEventService;
        _stripeEventUtilityService = stripeEventUtilityService;
        _organizationService = organizationService;
        _stripeFacade = stripeFacade;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _userService = userService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.SubscriptionUpdated"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var subscription = await _stripeEventService.GetSubscription(parsedEvent, true, ["customer", "discounts"]);
        var (organizationId, userId, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        switch (subscription.Status)
        {
            case StripeSubscriptionStatus.Unpaid or StripeSubscriptionStatus.IncompleteExpired
                when organizationId.HasValue:
                {
                    await _organizationService.DisableAsync(organizationId.Value, subscription.CurrentPeriodEnd);
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
                    await _organizationService.EnableAsync(organizationId.Value);
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
    private async Task RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync(Event parsedEvent,
        Subscription subscription)
    {
        if (parsedEvent.Data.PreviousAttributes?.items is null)
        {
            return;
        }

        var previousSubscription = parsedEvent.Data
            .PreviousAttributes
            .ToObject<Subscription>() as Subscription;

        // This being false doesn't necessarily mean that the organization doesn't subscribe to Secrets Manager.
        // If there are changes to any subscription item, Stripe sends every item in the subscription, both
        // changed and unchanged.
        var previousSubscriptionHasSecretsManager = previousSubscription?.Items is not null &&
                                                    previousSubscription.Items.Any(previousItem =>
                                                        StaticStore.Plans.Any(p =>
                                                            p.SecretsManager is not null &&
                                                            p.SecretsManager.StripeSeatPlanId ==
                                                            previousItem.Plan.Id));

        var currentSubscriptionHasSecretsManager = subscription.Items.Any(i =>
            StaticStore.Plans.Any(p =>
                p.SecretsManager is not null &&
                p.SecretsManager.StripeSeatPlanId == i.Plan.Id));

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
}
