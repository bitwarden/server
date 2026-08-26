using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessAuditEventRepository
{
    /// <summary>
    /// Appends one event to the PAM audit store. State-changing PAM actions call this through the audit-event emitter at
    /// the moment an action happens: an <c>Attempt</c> before the action and an <c>Outcome</c> after. The store is
    /// append-only (no update or delete); a generated identifier is assigned here.
    /// </summary>
    Task CreateAsync(AccessAuditEventData auditEvent);

    /// <summary>
    /// Returns one page of the PAM access-audit trail for an entire organization: stored events occurring on or after
    /// <paramref name="since"/>, newest first, carrying the display names snapshotted at write time. The trail is
    /// org-wide (the caller is authorized by the AccessEventLogs permission at the endpoint, not by collection
    /// management), so the access-request, access-lease, and rule-administration kinds are all included.
    /// </summary>
    /// <remarks>
    /// Paging is keyset rather than offset: pass the last event of the previous page as <paramref name="before"/> to
    /// get the next one. An offset would re-serve rows, because the store is append-only and read newest first, so
    /// every event written between two requests shifts the window down by one. It would also get slower with depth,
    /// since the database has to read and discard the skipped rows.
    /// </remarks>
    Task<ICollection<AccessAuditEvent>> GetManyByOrganizationIdAsync(
        Guid organizationId, DateTime since, AccessAuditEventCursor? before, int take);
}
