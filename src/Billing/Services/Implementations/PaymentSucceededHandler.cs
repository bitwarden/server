using Bit.Billing.Constants;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class PaymentSucceededHandler(
    ILogger<PaymentSucceededHandler> logger,
    IStripeEventService stripeEventService,
    IStripeFacade stripeFacade,
    IProviderRepository providerRepository,
    IOrganizationRepository organizationRepository,
    IStripeEventUtilityService stripeEventUtilityService,
    IUserService userService,
    IOrganizationEnableCommand organizationEnableCommand,
    IPricingClient pricingClient,
    IPushNotificationAdapter pushNotificationAdapter)
    : IPaymentSucceededHandler
{
    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.PaymentSucceeded"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var invoice = await stripeEventService.GetInvoice(parsedEvent, true);
        if (invoice.Status != StripeConstants.InvoiceStatus.Paid || invoice.BillingReason != "subscription_create")
        {
            return;
        }

        if (invoice.Parent?.SubscriptionDetails == null)
        {
            return;
        }

        var subscription = await stripeFacade.GetSubscription(invoice.Parent.SubscriptionDetails.SubscriptionId);
        if (subscription?.Status != StripeSubscriptionStatus.Active)
        {
            return;
        }

        if (DateTime.UtcNow - invoice.Created < TimeSpan.FromMinutes(1))
        {
            await Task.Delay(5000);
        }

        var (organizationId, userId, providerId) = stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);

        if (providerId.HasValue)
        {
            var provider = await providerRepository.GetByIdAsync(providerId.Value);

            if (provider == null)
            {
                logger.LogError(
                    "Received invoice.payment_succeeded webhook ({EventID}) for Provider ({ProviderID}) that does not exist",
                    parsedEvent.Id,
                    providerId.Value);

                return;
            }

            var teamsMonthly = await pricingClient.GetPlanOrThrow(PlanType.TeamsMonthly);

            var enterpriseMonthly = await pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly);

            var teamsMonthlyLineItem =
                subscription.Items.Data.FirstOrDefault(item =>
                    item.Plan.Id == teamsMonthly.PasswordManager.StripeSeatPlanId);

            var enterpriseMonthlyLineItem =
                subscription.Items.Data.FirstOrDefault(item =>
                    item.Plan.Id == enterpriseMonthly.PasswordManager.StripeSeatPlanId);

            if (teamsMonthlyLineItem == null || enterpriseMonthlyLineItem == null)
            {
                logger.LogError("invoice.payment_succeeded webhook ({EventID}) for Provider ({ProviderID}) indicates missing subscription line items",
                    parsedEvent.Id,
                    provider.Id);
            }
        }
        else if (organizationId.HasValue)
        {
            var organization = await organizationRepository.GetByIdAsync(organizationId.Value);

            if (organization == null)
            {
                return;
            }

            var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

            if (subscription.Items.All(item => plan.PasswordManager.StripePlanId != item.Plan.Id))
            {
                return;
            }

            await organizationEnableCommand.EnableAsync(organizationId.Value, subscription.GetCurrentPeriodEnd());
            organization = await organizationRepository.GetByIdAsync(organization.Id);
            await pushNotificationAdapter.NotifyEnabledChangedAsync(organization!);
        }
        else if (userId.HasValue)
        {
            if (subscription.Items.All(i => i.Plan.Id != IStripeEventUtilityService.PremiumPlanId))
            {
                return;
            }

            await userService.EnablePremiumAsync(userId.Value, subscription.GetCurrentPeriodEnd());
        }
    }
}
