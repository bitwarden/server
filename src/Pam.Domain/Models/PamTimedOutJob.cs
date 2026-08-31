using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// One job the sweep moved to <see cref="PamRotationJobStatus.TimedOut"/> because it was still Pending or Claimed
/// past <c>ExpiresAt</c> with no successful attempt — the row the sweep needs to emit the <c>timed_out</c> audit
/// event with its unroutable-vs-stuck reason.
/// </summary>
public record PamTimedOutJob
{
    public required Guid JobId { get; init; }
    public required Guid RotationConfigId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid CipherId { get; init; }
    public required PamRotationSource Source { get; init; }

    /// <summary>The daemon holding the claim at timeout, or null if the job was never claimed.</summary>
    public Guid? ClaimedByDaemonId { get; init; }

    /// <summary>The number of attempts recorded against the job: zero means unroutable (never claimed), nonzero means stuck.</summary>
    public required int AttemptCount { get; init; }
}
