namespace Bit.Pam.Models;

/// <summary>
/// A lease the natural-expiry sweep found newly ended, for the LeaseExpired audit event and the rotation access-end
/// trigger.
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
