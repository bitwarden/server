namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>
/// The diff counts shown after a commit. Inserted/Updated/Unassigned are mapped from the
/// sync proc's result set; PlanMismatch is computed in C# (migration cohorts only) and is
/// informational: it never blocks.
/// </summary>
public class CohortBulkAssignmentSummary
{
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Unassigned { get; set; }
    public int PlanMismatch { get; set; }
}
