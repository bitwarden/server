namespace Bit.Pam.Enums;

/// <summary>
/// The phase of a PAM audit event under the before/after (write-ahead) emission model. Emission is not
/// transactional, so an <see cref="Attempt"/> with no matching <see cref="Outcome"/> marks an in-doubt action
/// rather than a silently lost event.
/// </summary>
public enum AccessAuditEventPhase : byte
{
    /// <summary>Recorded before the action's point of no return: the action was attempted.</summary>
    Attempt = 0,

    /// <summary>Recorded after the action completed: its result (success, or a recorded rejection).</summary>
    Outcome = 1,
}
