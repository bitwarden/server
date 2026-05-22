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

    /// <summary>
    /// Returns the number of assignments in the given cohort that have left the Pending state.
    /// For Migration cohorts (MigrationPathId IS NOT NULL), this counts assignments with a
    /// ScheduledDate set (covers both Scheduled and Migrated). For Churn-only cohorts
    /// (MigrationPathId IS NULL), this counts assignments with a ChurnDiscountAppliedDate set
    /// (redeemed save-offers). A non-zero count means the cohort has historical activity:
    /// deletion is refused and the migration path is locked against further edits.
    /// </summary>
    Task<int> GetCohortNonPendingAssignmentsCountAsync(Guid cohortId);
}
