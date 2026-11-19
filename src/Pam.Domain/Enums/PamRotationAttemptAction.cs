namespace Bit.Pam.Enums;

/// <summary>
/// What the claiming access connector reported for a <see cref="Entities.PamRotationAttempt"/>, if anything. Executing
/// vs Abandoned is derived by <see cref="PamRotationStatusDerivation.ComputeAttemptStatus"/>, never stored.
/// </summary>
public enum PamRotationAttemptAction : byte
{
    /// <summary>No report yet. Executing while the attempt holds its job's live claim, Abandoned once that claim is
    /// released or times out.</summary>
    None = 0,

    /// <summary>The access connector reported success and the <c>VerifiedBeforeSuccess</c> backstop
    /// (<see cref="Entities.PamRotationAttempt.CipherUpdated"/>) held.</summary>
    Rotated = 1,

    /// <summary>The access connector reported failure. The only outcome the retry budget counts.</summary>
    Errored = 2,
}
