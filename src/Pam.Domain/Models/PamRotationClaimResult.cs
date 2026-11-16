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

    /// <summary>The target system's sealed blob.</summary>
    public string? TargetSystemSealedData { get; init; }

    /// <summary>The target system's wrapped content key.</summary>
    public string? TargetSystemWrappedKey { get; init; }

    public Guid? CipherId { get; init; }

    /// <summary>The rotation config's sealed blob.</summary>
    public string? RotationConfigSealedData { get; init; }

    /// <summary>The rotation config's wrapped content key.</summary>
    public string? RotationConfigWrappedKey { get; init; }

    /// <summary><c>ClaimedDate + ReleaseDelay</c>; the access connector should finish before then.</summary>
    public DateTime? ExecuteBy { get; init; }
}
