using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Queries;

namespace Bit.Subscriptions.Organization.Handlers;

internal sealed class PurchasePrivilegedControlsHandler(
    IOrganizationRepository organizationRepository,
    IPricingClient pricingClient,
    IIsOnPrivilegedControlsSalesInitiatedTrialQuery isOnPrivilegedControlsSalesInitiatedTrialQuery,
    IAddPrivilegedControlsToExistingSubscriptionCommand addPrivilegedControlsCommand)
{
    public async Task HandleAsync(Guid organizationId, PurchasePrivilegedControlsRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        // Checked first so a trial organization without a subscription gets this message, not the checks below.
        if (await isOnPrivilegedControlsSalesInitiatedTrialQuery.Run(organization))
        {
            throw new BadRequestException(
                "Work with your customer service representative to purchase Privileged Controls.");
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsPrivilegedControls || plan.ProductTier != ProductTierType.Enterprise)
        {
            throw new BadRequestException("Privileged Controls is only available on Enterprise organizations.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        await addPrivilegedControlsCommand.Run(organization, request.Seats!.Value, request.MaxAutoscaleSeats);
    }
}
