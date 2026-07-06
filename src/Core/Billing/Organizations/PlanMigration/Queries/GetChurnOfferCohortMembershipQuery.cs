using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;

namespace Bit.Core.Billing.Organizations.PlanMigration.Queries;

public class GetChurnOfferCohortMembershipQuery(
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository) : IGetChurnOfferCohortMembershipQuery
{
    public async Task<ChurnOfferCohortMembership?> Run(Organization organization)
    {
        var assignment = await assignmentRepository.GetByOrganizationIdAsync(organization.Id);
        if (assignment is null)
        {
            return null;
        }

        var cohort = await cohortRepository.GetByIdAsync(assignment.CohortId);
        if (cohort is not { IsActive: true } || string.IsNullOrEmpty(cohort.ChurnDiscountCouponCode))
        {
            return null;
        }

        return new ChurnOfferCohortMembership(assignment, cohort);
    }
}
