using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.PlanMigration.Repositories;

public interface IOrganizationPlanMigrationCohortAssignmentRepository
    : IRepository<OrganizationPlanMigrationCohortAssignment, Guid>
{
    /// <summary>
    /// Returns the single assignment for the given organization, or <c>null</c> when the
    /// organization has not been assigned to any cohort. At most one assignment per
    /// organization is enforced by a UNIQUE constraint at the database layer.
    /// </summary>
    Task<OrganizationPlanMigrationCohortAssignment?> GetByOrganizationIdAsync(Guid organizationId);
}
