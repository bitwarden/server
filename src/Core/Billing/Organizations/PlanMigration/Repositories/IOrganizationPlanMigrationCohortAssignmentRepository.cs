using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.PlanMigration.Repositories;

public interface IOrganizationPlanMigrationCohortAssignmentRepository
    : IRepository<OrganizationPlanMigrationCohortAssignment, Guid>
{
    Task<OrganizationPlanMigrationCohortAssignment?> GetByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the number of assignments in the given cohort that have left the Pending state.
    /// For Migration cohorts (MigrationPathId IS NOT NULL), this counts assignments with a
    /// ScheduledDate or MigratedDate set (covers both Scheduled and already-Migrated). For Churn-only cohorts
    /// (MigrationPathId IS NULL), this counts assignments with a ChurnDiscountAppliedDate set
    /// (redeemed save-offers). A non-zero count means the cohort has historical activity:
    /// deletion is refused and the migration path is locked against further edits.
    /// </summary>
    Task<int> GetCohortNonPendingAssignmentsCountAsync(Guid cohortId);

    /// <summary>
    /// Per-CSV-scoped bulk sync of assignments: inserts new rows, moves existing rows to a new
    /// cohort, and deletes rows whose source <c>CohortId</c> is null (un-assign sentinel).
    /// Organizations not present in <paramref name="rows"/> are left untouched. The MERGE is a
    /// single atomic statement and returns the Insert/Update/Unassign counts. MSSQL/Dapper only.
    /// </summary>
    Task<CohortBulkAssignmentSummary> SyncManyAsync(IEnumerable<ResolvedCohortBulkAssignmentRow> rows);
}
