using Bit.Pam.Models;

namespace Bit.Pam.Services;

/// <summary>
/// Records a PAM audit event at the moment a state-changing action happens (a request submitted or decided; a lease
/// activated, extended, or revoked; a rule created, updated, or deleted). This is the write side of the
/// access-audit trail; the governance trail is read back from the store these events are written to.
///
/// The default implementation (<see cref="NoopAccessAuditEventEmitter"/>) records nothing; the commercial
/// implementation persists each event to the dedicated PAM audit store (fanning out to the organization's normal
/// event log is deferred). Emitting behind this seam means call sites do not change as that write side evolves.
/// </summary>
public interface IAccessAuditEventEmitter
{
    /// <summary>
    /// Emits a single audit event. Callers await it as part of the action, but it never alters the action's outcome.
    /// </summary>
    Task EmitAsync(AccessAuditEventData auditEvent);
}
