namespace Bit.Pam.Enums;

/// <summary>
/// The result of the atomic write-capability check in <c>PamRotationAttempt_AcceptCipherWrite</c> — the security
/// backstop that re-verifies the claim, the attempt, and the cipher's revision date under a single lock before
/// writing the rotated secret.
/// </summary>
public enum PamRotationCipherWriteOutcome
{
    /// <summary>
    /// The write capability held and the cipher's <c>Data</c> and revision date were replaced (stored proc returned
    /// 1). <see cref="Entities.PamRotationAttempt.CipherUpdated"/> is set.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The job is not Claimed by the calling daemon, or the attempt is not
    /// <see cref="PamRotationAttemptStatus.Executing"/> (stored proc returned 0) — the complement of spec
    /// <c>AcceptCipherUpdate</c>, audited as <c>write_rejected</c>. Nothing was persisted.
    /// </summary>
    Rejected = 0,

    /// <summary>
    /// The write capability held but the caller's supplied last-known revision date no longer matched the cipher's
    /// current revision date (stored proc returned -1) — a concurrent user edit won. Outside spec
    /// <c>RejectCipherUpdate</c>'s exact complement; added to protect concurrent user edits. Audited as
    /// <c>write_rejected</c>, nothing was persisted.
    /// </summary>
    RevisionMismatch = -1,
}
