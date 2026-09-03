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
    /// Returns one page of the PAM access-audit trail for an entire organization: the stored events matching
    /// <paramref name="filter"/>, newest first, at most <see cref="AccessAuditTrailFilter.PageSize"/> of them, carrying
    /// the display names snapshotted at write time. The trail is org-wide (the caller is authorized by the
    /// AccessEventLogs permission at the endpoint, not by collection management), so the access-request, access-lease,
    /// and rule-administration kinds are all included.
    ///
    /// Each action's before/after pair is already collapsed here, in the store, rather than by the caller: the caller
    /// sees one page and could not tell an <c>Attempt</c> whose <c>Outcome</c> sits on the next page from one that
    /// never landed. What survives the collapse is the <c>Outcome</c> where the action completed, and the lone
    /// <c>Attempt</c> where it did not — which the caller then flags as in-doubt. The collapse is scoped to the
    /// filter's own range, so an action straddling a range bound reads as in-doubt at that edge rather than vanishing.
    ///
    /// The dimensions are applied to whichever row survived the collapse, because the two halves of one action need
    /// not agree: a refused activation writes its <c>Attempt</c> as <c>LeaseActivated</c> and its <c>Outcome</c> as
    /// <c>LeaseActivationRejected</c>, so filtering first would answer "activated" with an action that was turned down.
    /// </summary>
    /// <remarks>
    /// Paging is keyset rather than offset: pass the last event of the previous page as
    /// <see cref="AccessAuditTrailFilter.Before"/> to get the next one. An offset would re-serve rows, because the
    /// store is append-only and read newest first, so every event written between two requests shifts the window down
    /// by one. It would also get slower with depth, since the database has to read and discard the skipped rows.
    /// </remarks>
    Task<ICollection<AccessAuditEvent>> GetPageByOrganizationIdAsync(
        Guid organizationId, AccessAuditTrailFilter filter);

    /// <summary>
    /// Returns the distinct subjects — ciphers and access rules — the organization's trail names between
    /// <paramref name="since"/> and <paramref name="until"/>, one row per subject.
    ///
    /// This is what the trail's Item filter is built from. It cannot come from a page of the trail, which holds only a
    /// page's worth of rows where the menu has to offer every item in range, and it cannot come from the caller's vault
    /// either, since that would offer every credential they hold whether or not the trail ever mentions it. Reading the
    /// distinct subjects and letting the caller keep the ones it can name is the only version that offers exactly the
    /// items that both occur and can be labelled.
    ///
    /// Meant to be scoped to the same range the page read uses, so the menu cannot offer an option the page can never
    /// match.
    /// </summary>
    Task<ICollection<AccessAuditItem>> GetItemsByOrganizationIdAsync(
        Guid organizationId, DateTime since, DateTime until);
}
