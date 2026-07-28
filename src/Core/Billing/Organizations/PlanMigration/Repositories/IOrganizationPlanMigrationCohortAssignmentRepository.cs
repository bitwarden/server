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

    /// <summary>
    /// Returns a single bounded keyset page of export rows for the given cohort, joined to the
    /// organization to surface <see cref="CohortAssignmentExportRow.OrganizationName"/>. Rows are
    /// ordered by <c>(CreationDate, Id)</c> using the provider's native ordering; the cursor is only
    /// internally consistent (the seek matches the ORDER BY on a given provider). Row order is not
    /// guaranteed identical across databases, which is acceptable because the export is consumed as
    /// a download. A page shorter than <paramref name="take"/> signals the end.
    /// </summary>
    /// <param name="cohortId">The cohort whose assignments to export.</param>
    /// <param name="afterCreationDate">Exclusive lower bound on <c>CreationDate</c>; null for the first page.</param>
    /// <param name="afterId">Tiebreaker lower bound on <c>Id</c> when <c>CreationDate</c> ties; null for the first page.</param>
    /// <param name="take">Maximum number of rows to return.</param>
    Task<IReadOnlyList<CohortAssignmentExportRow>> GetExportRowsByCohortIdAsync(
        Guid cohortId, DateTime? afterCreationDate, Guid? afterId, int take);

    /// <summary>
    /// Returns a list of assignments that are candidates for sending invoices,
    /// based on the ExpirationDate of the organization's current plan.
    /// This is used to identify which organizations should receive invoice notifications for their upcoming plan migrations.
    /// </summary>
    /// <param name="minDays">The minimum number of days from today for the ExpirationDate.</param>
    /// <param name="maxDays">The maximum number of days from today for the ExpirationDate.</param>
    /// <returns>A list of assignments that are eligible for sending invoices.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="minDays"/> is greater than <paramref name="maxDays"/>.</exception>
    Task<IReadOnlyList<OrganizationPlanMigrationCohortAssignment>> GetSendInvoiceCandidatesInWindowAsync(int minDays, int maxDays);
}
