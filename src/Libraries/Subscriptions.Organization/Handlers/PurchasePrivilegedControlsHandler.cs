using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.Extensions.Logging;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class PurchasePrivilegedControlsHandler(
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ILogger<PurchasePrivilegedControlsHandler> logger,
    IAddPrivilegedControlsToExistingSubscriptionCommand addPrivilegedControlsCommand)
{
    public async Task HandleAsync(Guid organizationId, PurchasePrivilegedControlsRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        // Checked first so a trial organization without a subscription gets this message, not the checks below.
        var subscription =
            plan.SupportsPrivilegedControls && organization.UsePam &&
            !string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId)
                ? await OrganizationSubscriptionHelpers.TryGetSubscriptionAsync(stripeAdapter, logger, organization)
                : null;

        if (PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(organization, plan, subscription))
        {
            throw new BadRequestException(
                "Work with your customer service representative to purchase Privileged Controls.");
        }

        if (!plan.SupportsPrivilegedControls || plan.ProductTier != ProductTierType.Enterprise)
        {
            throw new BadRequestException("Privileged Controls is only available on Enterprise organizations.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        await addPrivilegedControlsCommand.Run(organization, request.Seats, request.MaxAutoscaleSeats);
    }
}
