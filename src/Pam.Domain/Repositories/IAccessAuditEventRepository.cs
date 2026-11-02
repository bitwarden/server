using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessAuditEventRepository
{
    /// <summary>
    /// Appends one event. The store is append-only: no update, no delete, and the identifier is assigned here.
    /// </summary>
    Task CreateAsync(AccessAuditEventData auditEvent);

    /// <summary>
    /// One page of the organization's trail, newest first. The trail is org-wide: the caller is authorized by the
    /// AccessEventLogs permission at the endpoint, not by collection management.
    ///
    /// Each action's <c>Attempt</c>/<c>Outcome</c> pair is collapsed here rather than by the caller, which sees one
    /// page and could not tell an <c>Attempt</c> whose <c>Outcome</c> sits on the next page from one that never
    /// landed. What survives is the <c>Outcome</c> where the action completed and the lone <c>Attempt</c> where it
    /// did not, which the caller flags as in-doubt. The dimensions then apply to whichever row survived, because the
    /// two halves need not agree: a refused activation writes <c>LeaseActivated</c> then
    /// <c>LeaseActivationRejected</c>, so filtering first would answer "activated" with an action that was refused.
    /// </summary>
    /// <remarks>
    /// Paging is keyset: pass the last event of the previous page as
    /// <see cref="AccessAuditTrailFilter.Before"/>. An offset would re-serve rows, since the store is append-only and
    /// read newest first, and would get slower with depth.
    /// </remarks>
    Task<ICollection<AccessAuditEvent>> GetPageByOrganizationIdAsync(
        Guid organizationId, AccessAuditTrailFilter filter);

    /// <summary>
    /// What the trail's Item filter is built from. A page of the trail holds too few rows to name every item in
    /// range, and the caller's own vault would offer every credential they hold whether the trail mentions it or not.
    /// Scope it to the same range as the page read, so the menu cannot offer an option the page can never match.
    /// </summary>
    Task<ICollection<AccessAuditItem>> GetItemsByOrganizationIdAsync(
        Guid organizationId, DateTime since, DateTime until);
}
