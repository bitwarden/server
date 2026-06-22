namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

/// <summary>
/// A single organization's row in a cohort CSV export. Pure data: the export query yields these,
/// and the Admin controller is responsible for CSV formatting and HTTP streaming. Contains no
/// Vault Data -- only org-level operational metadata.
/// </summary>
/// <param name="Id">
/// The assignment <c>Id</c>. Used only as the keyset-paging tiebreaker; it is NOT written to the CSV.
/// </param>
/// <param name="OrganizationName">The organization's display name (joined from the Organization table).</param>
/// <param name="AssignedAt">
/// The date the organization was assigned to the cohort (the assignment <c>CreationDate</c>).
/// </param>
public record CohortAssignmentExportRow(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    DateTime AssignedAt,
    DateTime? ScheduledDate,
    DateTime? MigratedDate);
