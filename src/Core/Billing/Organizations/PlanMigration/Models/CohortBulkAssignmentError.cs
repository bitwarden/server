namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>A single line-scoped validation error surfaced to the operator.</summary>
public record CohortBulkAssignmentError(int LineNumber, string Message);
