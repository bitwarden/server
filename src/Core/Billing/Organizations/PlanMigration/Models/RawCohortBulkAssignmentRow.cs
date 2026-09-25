namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>One parsed CSV data row (raw strings + 1-based file line number).</summary>
public record RawCohortBulkAssignmentRow(int LineNumber, string OrganizationId, string CohortName);
