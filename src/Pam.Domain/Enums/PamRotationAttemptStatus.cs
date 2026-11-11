namespace Bit.Pam.Enums;

/// <summary>
/// Where a <see cref="Entities.PamRotationAttempt"/> stands, derived at read time by
/// <see cref="PamRotationStatusDerivation.ComputeAttemptStatus"/> and never stored.
/// </summary>
public enum PamRotationAttemptStatus : byte
{
    /// <summary>At most one per job (<c>AtMostOneInFlightAttemptPerJob</c>).</summary>
    Executing = 0,

    /// <summary>
    /// Success was reported after the cipher write (<see cref="Entities.PamRotationAttempt.CipherUpdated"/>).
    /// </summary>
    Rotated = 1,

    Errored = 2,

    /// <summary>The job was released or timed out mid-attempt; not charged to the retry budget.</summary>
    Abandoned = 3,
}
