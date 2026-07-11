namespace Bit.Pam.Models;

/// <summary>
/// One lease the natural-expiry sweep flipped from <see cref="Enums.AccessLeaseStatus.Active"/> to
/// <see cref="Enums.AccessLeaseStatus.Expired"/> because its window closed on its own (no revoke or cancel
/// involved) — the row <c>IAccessLeaseRepository.ExpireDueAsync</c> returns for the deferred LeaseExpired audit
/// event and the rotation access-end trigger.
/// </summary>
public record PamExpiredLease
{
    public required Guid Id { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid CollectionId { get; init; }
    public required Guid CipherId { get; init; }
    public required Guid RequesterId { get; init; }
    public required DateTime NotBefore { get; init; }
    public required DateTime NotAfter { get; init; }
}
