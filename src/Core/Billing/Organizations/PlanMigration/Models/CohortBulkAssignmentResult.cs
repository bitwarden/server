namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>
/// Outcome of validating (and, when clean, committing) a bulk upload. When
/// <see cref="Errors"/> is non-empty, nothing was written and <see cref="Summary"/> is null.
/// </summary>
public class CohortBulkAssignmentResult
{
    public CohortBulkAssignmentSummary? Summary { get; init; }
    public IReadOnlyList<CohortBulkAssignmentError> Errors { get; init; } = [];
    public bool Succeeded => Errors.Count == 0;
}
