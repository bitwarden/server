using Bit.Pam.Models;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Records PAM audit events for state-changing actions; the write side of the access-audit trail.
/// </summary>
public interface IAccessAuditEventEmitter
{
    /// <summary>Emits a single audit event.</summary>
    Task EmitAsync(AccessAuditEventData auditEvent);
}
