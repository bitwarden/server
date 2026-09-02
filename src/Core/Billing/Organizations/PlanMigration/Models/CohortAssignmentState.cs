namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>
/// A snapshot of how many of a cohort's assignments have left the Pending state.
/// Consumers interpret <see cref="HasNonPendingAssignments"/> as the trigger for downstream
/// rules: locking the migration path on the Edit form, refusing deletion, etc.
/// </summary>
public record CohortAssignmentState(int NonPendingAssignmentCount)
{
    public bool HasNonPendingAssignments => NonPendingAssignmentCount > 0;
}
