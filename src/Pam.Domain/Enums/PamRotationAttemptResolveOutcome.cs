namespace Bit.Pam.Enums;

/// <summary>
/// The result of resolving a <see cref="Entities.PamRotationAttempt"/> (the <c>_MarkRotated</c> / <c>_MarkErrored</c>
/// stored procedures). Both guard on the attempt still being <see cref="PamRotationAttemptStatus.Executing"/> and
/// claimed by the reporting daemon (spec <c>RejectStaleSuccess</c> / <c>RejectStaleFailureReport</c>) and return a
/// distinct integer code so the caller takes the reject-stale path instead of resolving.
/// </summary>
public enum PamRotationAttemptResolveOutcome
{
    /// <summary>The guard held and the attempt was resolved (stored proc returned 1).</summary>
    Resolved = 1,

    /// <summary>
    /// The attempt was not <see cref="PamRotationAttemptStatus.Executing"/>, or its
    /// <see cref="Entities.PamRotationAttempt.ClaimedByDaemonId"/> did not match the reporting daemon (stored proc
    /// returned 0) — the report is stale. Audited as <c>report_rejected</c>; nothing changed.
    /// </summary>
    Rejected = 0,
}
