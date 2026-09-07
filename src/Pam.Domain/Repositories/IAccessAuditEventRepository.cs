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
    /// Returns one page of the PAM access-audit trail for an entire organization: the stored events matching
    /// <paramref name="filter"/>, newest first, at most <see cref="AccessAuditTrailFilter.PageSize"/> of them,
    /// with display names joined on read.
    /// </summary>
    /// <remarks>
    /// Each action's before/after pair is collapsed here, in the store, rather than by the caller, since the
    /// caller sees only one page and cannot tell an <c>Attempt</c> whose <c>Outcome</c> sits on the next page
    /// from one that never landed. What survives is the <c>Outcome</c> if the action completed, otherwise the
    /// lone <c>Attempt</c>, flagged in-doubt by the response. The collapse is scoped to the filter's own range,
    /// so an action straddling a range bound reads as in-doubt at that edge rather than vanishing. Ordered by
    /// <c>OccurredAt</c> descending, broken by row id — the order <see cref="AccessAuditTrailFilter.BeforeOccurredAt"/>
    /// resumes from.
    /// </remarks>
    Task<ICollection<AccessAuditEvent>> GetPageByOrganizationIdAsync(
        Guid organizationId, AccessAuditTrailFilter filter);

    /// <summary>
    /// Returns the distinct subjects — ciphers and access rules — the organization's trail names between
    /// <paramref name="since"/> and <paramref name="until"/>, one row per subject. This is what the trail's
    /// Item filter menu is built from, scoped to the same range the page read uses.
    /// </summary>
    Task<ICollection<AccessAuditItem>> GetItemsByOrganizationIdAsync(
        Guid organizationId, DateTime since, DateTime until);
}
