using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Records PAM audit events for state-changing actions; the write side of the access-audit trail.
/// </summary>
public interface IAccessAuditEventEmitter
{
    /// <summary>Emits a single audit event.</summary>
    /// <remarks>
    /// A failed <see cref="AccessAuditEventPhase.Attempt"/> throws, so the action does not proceed. A failed
    /// <see cref="AccessAuditEventPhase.Outcome"/> is logged and swallowed, since the action has already happened.
    /// </remarks>
    Task EmitAsync(AccessAuditEventData auditEvent);
}
