namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>
/// A validated row ready for the sync MERGE. A null <see cref="CohortId"/> is the
/// un-assignment sentinel (empty cohort cell). The insert PK is generated in the
/// repository, not here.
/// </summary>
public record ResolvedCohortBulkAssignmentRow(Guid OrganizationId, Guid? CohortId);
