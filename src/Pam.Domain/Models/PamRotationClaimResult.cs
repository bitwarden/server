using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The result of a claim. On <see cref="PamRotationClaimOutcome.Claimed"/> the other fields carry the work snapshot
/// for the access connector; otherwise they are null.
/// </summary>
public class PamRotationClaimResult
{
    public required PamRotationClaimOutcome Outcome { get; init; }

    public Guid? AttemptId { get; init; }

    public Guid? JobId { get; init; }
    public PamRotationSource? Source { get; init; }
    public Guid? TargetSystemId { get; init; }
    public string? TargetSystemName { get; init; }
    public PamTargetSystemKind? Kind { get; init; }

    /// <summary>A serialized <see cref="PamPasswordPolicy"/>.</summary>
    public string? PasswordPolicy { get; init; }

    public Guid? CipherId { get; init; }
    public string? AccountIdentity { get; init; }
    public bool? TerminateSessions { get; init; }

    /// <summary><c>ClaimedAt + ReleaseDelay</c>; the access connector should finish before then.</summary>
    public DateTime? ExecuteBy { get; init; }
}
