namespace Bit.Services.Pam.Models;

/// <summary>
/// A request to extend an active lease in place by <see cref="DurationSeconds"/>. Extensions are approved
/// automatically, once per lease, when the governing rule allows them; <see cref="Reason"/> is required.
/// </summary>
public sealed class AccessLeaseExtensionSubmission
{
    public Guid LeaseId { get; init; }
    public int DurationSeconds { get; init; }
    public string? Reason { get; init; }
}
