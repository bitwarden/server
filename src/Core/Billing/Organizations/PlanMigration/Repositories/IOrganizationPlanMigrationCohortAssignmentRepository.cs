using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.PlanMigration.Repositories;

public interface IOrganizationPlanMigrationCohortAssignmentRepository
    : IRepository<OrganizationPlanMigrationCohortAssignment, Guid>
{
    Task<OrganizationPlanMigrationCohortAssignment?> GetByOrganizationIdAsync(Guid organizationId);
}
