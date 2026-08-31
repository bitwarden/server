using Bit.Pam.Models;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Records a PAM audit event at the moment a state-changing action happens (a request submitted or decided; a lease
/// activated, extended, or revoked). This is the write side of the access-audit trail.
///
/// Every state-changing PAM command emits through this seam, so the call sites do not depend on where the events land.
/// <see cref="AccessAuditEventEmitter"/> appends them to the dedicated append-only audit store, which the access-audit
/// trail is read back from, and copies the subset that has an organization-wide equivalent into the organization's
/// event log.
/// </summary>
public interface IAccessAuditEventEmitter
{
    /// <summary>
    /// Emits a single audit event. Callers await it as part of the action, but it never alters the action's outcome.
    /// </summary>
    Task EmitAsync(AccessAuditEventData auditEvent);
}
