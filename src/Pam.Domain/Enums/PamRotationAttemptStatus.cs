namespace Bit.Pam.Enums;

/// <summary>
/// Where a <see cref="Entities.PamRotationAttempt"/> stands, derived at read time by
/// <see cref="PamRotationStatusDerivation.ComputeAttemptStatus"/> and never stored.
/// </summary>
public enum PamRotationAttemptStatus : byte
{
    /// <summary>The claiming access connector is working the job. At most one per job — invariant
    /// <c>AtMostOneInFlightAttemptPerJob</c>.</summary>
    Executing = 0,

    /// <summary>The access connector reported success and the <c>VerifiedBeforeSuccess</c> backstop
    /// (<see cref="Entities.PamRotationAttempt.CipherUpdated"/>) held.</summary>
    Rotated = 1,

    /// <summary>The access connector reported failure.</summary>
    Errored = 2,

    /// <summary>The job was released or timed out while this attempt was executing; the retry budget is not charged for
    /// it.</summary>
    Abandoned = 3,
}
