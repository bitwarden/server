namespace Bit.Core.Pam.Models;

/// <summary>
/// A request to extend an active lease. Extensions are always auto-approved, subject to the governing rule's
/// <c>AllowsExtensions</c> / <c>MaxExtensions</c> settings: the lease's end is pushed out by
/// <see cref="DurationSeconds"/> in place (no new lease is minted), and a justifying <see cref="Reason"/> is required.
/// </summary>
public sealed class AccessLeaseExtensionSubmission
{
    public Guid LeaseId { get; init; }
    public int DurationSeconds { get; init; }
    public string? Reason { get; init; }
}
