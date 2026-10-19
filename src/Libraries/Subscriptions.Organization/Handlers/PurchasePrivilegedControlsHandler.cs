using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models.Requests;
using Stripe;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class PurchasePrivilegedControlsHandler(
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    IStripePaymentService paymentService,
    IAddPrivilegedControlsToExistingSubscriptionCommand addPrivilegedControlsCommand)
{
    public async Task HandleAsync(Guid organizationId, PurchasePrivilegedControlsRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        // The trial check runs first because a sales-initiated trial organization may have no Stripe subscription,
        // and the checks below would otherwise hide the representative message. The subscription only changes the
        // trial rule's outcome when the plan supports Privileged Controls and Privileged Controls is on.
        var subscriptionInfo =
            plan.SupportsPrivilegedControls && organization.UsePam &&
            !string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId)
                ? await paymentService.GetSubscriptionAsync(organization)
                : null;

        if (PrivilegedControlsTrial.IsSalesInitiated(organization, plan, subscriptionInfo))
        {
            throw new BadRequestException(
                "Work with your customer service representative to purchase Privileged Controls.");
        }

        if (!plan.SupportsPrivilegedControls || plan.ProductTier != ProductTierType.Enterprise)
        {
            throw new BadRequestException("Privileged Controls is only available on Enterprise organizations.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        try
        {
            await addPrivilegedControlsCommand.Run(organization, request.Seats, request.MaxAutoscaleSeats);
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Type == "card_error")
        {
            throw new BadRequestException(stripeException.StripeError.Message);
        }
    }
}
