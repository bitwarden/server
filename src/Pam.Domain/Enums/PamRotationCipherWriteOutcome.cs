namespace Bit.Pam.Enums;

/// <summary>
/// The result of <c>PamRotationAttempt_AcceptCipherWrite</c>, which re-verifies the claim, the attempt, and the
/// cipher's revision date under one lock before writing the rotated secret. Each value is the code it returns.
/// </summary>
public enum PamRotationCipherWriteOutcome
{
    /// <summary>
    /// The cipher's <c>Data</c> and revision date were replaced, and
    /// <see cref="Entities.PamRotationAttempt.CipherUpdated"/> is set.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The job is not Claimed by the calling access connector, or the attempt is not
    /// <see cref="PamRotationAttemptStatus.Executing"/>. Nothing is persisted.
    /// </summary>
    Rejected = 0,

    /// <summary>
    /// The supplied last-known revision date no longer matches the cipher's, so a concurrent user edit won. Nothing is
    /// persisted.
    /// </summary>
    RevisionMismatch = -1,
}
