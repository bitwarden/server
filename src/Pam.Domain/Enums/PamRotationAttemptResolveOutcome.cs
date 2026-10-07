namespace Bit.Pam.Enums;

/// <summary>
/// The result of resolving a <see cref="Entities.PamRotationAttempt"/>; each value is the code the
/// <c>_MarkRotated</c> and <c>_MarkErrored</c> stored procedures return.
/// </summary>
public enum PamRotationAttemptResolveOutcome
{
    Resolved = 1,

    /// <summary>
    /// The attempt is no longer <see cref="PamRotationAttemptStatus.Executing"/> under the reporting access
    /// connector's claim, or a success arrived before the cipher write. Nothing changes.
    /// </summary>
    Rejected = 0,
}
