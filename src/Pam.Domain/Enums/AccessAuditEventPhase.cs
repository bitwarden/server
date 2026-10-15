namespace Bit.Pam.Enums;

/// <summary>
/// A state-changing action records an <see cref="Attempt"/> before its point of no return and an
/// <see cref="Outcome"/> after it completes. Emission is not transactional, so an <see cref="Attempt"/> with no
/// matching <see cref="Outcome"/> marks an in-doubt action rather than a silently lost event.
/// </summary>
public enum AccessAuditEventPhase : byte
{
    Attempt = 0,
    Outcome = 1,
}
