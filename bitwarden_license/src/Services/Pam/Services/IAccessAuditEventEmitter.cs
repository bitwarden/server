using Bit.Pam.Models;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Records a PAM audit event at the moment a state-changing action happens (a request submitted or decided; a lease
/// activated, extended, or revoked). This is the write side of the access-audit trail.
///
/// Every state-changing PAM command emits through this seam, so the call sites do not change as the write side lands.
/// The only implementation today is <see cref="NoopAccessAuditEventEmitter"/>, which records nothing: the audit store
/// and the trail read model are a separate piece of work.
/// </summary>
public interface IAccessAuditEventEmitter
{
    /// <summary>
    /// Emits a single audit event. Callers await it as part of the action, but it never alters the action's outcome.
    /// </summary>
    Task EmitAsync(AccessAuditEventData auditEvent);
}
