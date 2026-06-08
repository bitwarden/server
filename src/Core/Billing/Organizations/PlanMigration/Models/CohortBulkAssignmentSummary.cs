namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>
/// The diff counts shown after a commit. Inserted/Updated/Unassigned and Skipped are mapped
/// from the sync proc's result set; PlanMismatch is computed in C# (migration cohorts only)
/// and is informational: it never blocks.
/// </summary>
public class CohortBulkAssignmentSummary
{
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Unassigned { get; set; }
    public int PlanMismatch { get; set; }

    /// <summary>
    /// Rows the CSV would have reassigned or unassigned but were skipped because the
    /// organization's existing assignment is locked (scheduled to migrate, or a churn
    /// discount has been applied). No-op rows (same cohort) are not counted.
    /// </summary>
    public int Skipped { get; set; }
}
