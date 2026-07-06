using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Queries;

public class GetAnnualUpgradeOfferQuery(
    IGetChurnOfferCohortMembershipQuery getChurnOfferCohortMembershipQuery,
    IPricingClient pricingClient,
    IOrganizationRepository organizationRepository) : IGetAnnualUpgradeOfferQuery
{
    public async Task<AnnualUpgradeOfferResult?> Run(Organization organization)
    {
        // Mutual exclusivity with the churn-mitigation coupon offer: membership in a churn-offer
        // -eligible cohort excludes this offer entirely, regardless of whether that offer is
        // currently live (e.g. its one-shot coupon may already be consumed).
        var membership = await getChurnOfferCohortMembershipQuery.Run(organization);
        if (membership is not null)
        {
            return null;
        }

        var annualLatestPlanType = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(organization.PlanType);
        if (annualLatestPlanType is null)
        {
            return null;
        }

        var currentPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var annualLatestPlan = await pricingClient.GetPlanOrThrow(annualLatestPlanType.Value);

        var seatCounts = await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);

        var currentAnnualCost = currentPlan.PasswordManager.SeatPrice * seatCounts.Total * 12;
        var newAnnualCost = annualLatestPlan.PasswordManager.SeatPrice * seatCounts.Total;
        var savings = currentAnnualCost - newAnnualCost;

        if (savings <= 0)
        {
            return null;
        }

        return new AnnualUpgradeOfferResult(currentAnnualCost, newAnnualCost, savings);
    }
}
