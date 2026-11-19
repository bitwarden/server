using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessAuditEventRepository
{
    /// <summary>Appends one event to the append-only store; the identifier is assigned here.</summary>
    Task CreateAsync(AccessAuditEventData auditEvent);

    /// <summary>
    /// One page of the organization's trail, newest first. Attempt/Outcome pairs collapse here, since one page cannot
    /// tell a missing Outcome from one on the next page; filters then apply to the survivor, as the halves can
    /// disagree.
    /// </summary>
    Task<ICollection<AccessAuditEvent>> GetPageByOrganizationIdAsync(
        Guid organizationId, AccessAuditTrailFilter filter);

    /// <summary>
    /// The subjects the trail's Item filter offers, drawn from the whole range since one page cannot name them all.
    /// Pass the page read's range, so the menu offers nothing the page cannot match.
    /// </summary>
    Task<ICollection<AccessAuditItem>> GetItemsByOrganizationIdAsync(
        Guid organizationId, DateTime since, DateTime until);
}
