using Bit.Pam.Models;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Records a PAM audit event for a state-changing action (a request submitted or decided; a lease activated,
/// extended, or revoked). The write side of the access-audit trail; call sites don't depend on where events land.
/// </summary>
public interface IAccessAuditEventEmitter
{
    /// <summary>
    /// Emits a single audit event. Callers await it as part of the action, but it never alters the action's outcome.
    /// </summary>
    Task EmitAsync(AccessAuditEventData auditEvent);
}
