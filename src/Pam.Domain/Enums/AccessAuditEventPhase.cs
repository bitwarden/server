namespace Bit.Pam.Enums;

/// <summary>
/// The phase of a PAM audit event under the before/after (write-ahead) emission model. A state-changing action records
/// an <see cref="Attempt"/> immediately before its point of no return and an <see cref="Outcome"/> immediately after it
/// completes. Because emission is not transactional, an <see cref="Attempt"/> with no matching <see cref="Outcome"/>
/// marks an in-doubt action (the process died mid-write) rather than a silently lost event.
/// </summary>
public enum AccessAuditEventPhase : byte
{
    /// <summary>Recorded before the action's point of no return: the action was attempted.</summary>
    Attempt = 0,

    /// <summary>Recorded after the action completed: its result (success, or a recorded rejection).</summary>
    Outcome = 1,
}
