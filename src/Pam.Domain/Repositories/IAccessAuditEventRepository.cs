using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessAuditEventRepository
{
    /// <summary>
    /// Appends one event to the PAM audit store. State-changing PAM actions call this through the audit-event emitter at
    /// the moment an action happens — an <c>Attempt</c> before the action and an <c>Outcome</c> after. The store is
    /// append-only (no update or delete); a generated identifier is assigned here.
    /// </summary>
    Task CreateAsync(AccessAuditEventData auditEvent);

    /// <summary>
    /// Returns the PAM access-audit trail for an entire organization — every stored event occurring on or after
    /// <paramref name="since"/>, newest first, with display names joined on read. The trail is org-wide (the caller is
    /// authorized by the AccessEventLogs permission at the endpoint, not by collection management), so the
    /// access-request, access-lease, and rule-administration kinds are all included.
    /// </summary>
    Task<ICollection<AccessAuditEvent>> GetManyByOrganizationIdAsync(Guid organizationId, DateTime since);
}
