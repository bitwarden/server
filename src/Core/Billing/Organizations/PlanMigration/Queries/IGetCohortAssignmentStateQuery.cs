using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;

namespace Bit.Core.Billing.Organizations.PlanMigration.Queries;

public interface IGetCohortAssignmentStateQuery
{
    Task<CohortAssignmentState> Run(OrganizationPlanMigrationCohort cohort);
}

public class GetCohortAssignmentStateQuery(
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository
) : IGetCohortAssignmentStateQuery
{
    public async Task<CohortAssignmentState> Run(OrganizationPlanMigrationCohort cohort)
    {
        var count = await assignmentRepository.GetCohortNonPendingAssignmentsCountAsync(cohort.Id);
        return new CohortAssignmentState(count);
    }
}
