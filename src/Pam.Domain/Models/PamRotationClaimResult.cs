using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The result of <c>IPamRotationJobRepository.ClaimAsync</c>. On <see cref="PamRotationClaimOutcome.Claimed"/> the
/// remaining fields carry the work snapshot handed back to the daemon (spec <c>ClaimRotation</c>'s snapshot); they
/// are null for any other <see cref="Outcome"/>.
/// </summary>
public class PamRotationClaimResult
{
    public required PamRotationClaimOutcome Outcome { get; init; }

    /// <summary>The Executing attempt created by the claim.</summary>
    public Guid? AttemptId { get; init; }

    public Guid? JobId { get; init; }
    public PamRotationSource? Source { get; init; }
    public Guid? TargetSystemId { get; init; }
    public string? TargetSystemName { get; init; }
    public PamTargetSystemKind? Kind { get; init; }

    /// <summary>The target's password policy, JSON — see <see cref="PamPasswordPolicy"/>.</summary>
    public string? PasswordPolicy { get; init; }

    public Guid? CipherId { get; init; }
    public string? AccountIdentity { get; init; }
    public bool? TerminateSessions { get; init; }

    /// <summary>The claim's lease deadline (<c>ClaimedAt + ReleaseDelay</c>) — the daemon should finish before this.</summary>
    public DateTime? ExecuteBy { get; init; }
}
