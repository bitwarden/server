using Bit.Pam.Models;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListAccessAuditTrailQuery
{
    /// <summary>
    /// Returns the synthesized, org-wide access-audit trail for <paramref name="organizationId"/>, within the shared
    /// history window, newest first — every access-request, access-lease, and rule-administration event in the
    /// organization. Authorization (the AccessEventLogs permission) is enforced at the endpoint before this runs.
    /// Events are projected from existing PAM entity state; nothing is persisted.
    /// </summary>
    Task<ICollection<AccessAuditEvent>> GetTrailAsync(Guid organizationId);
}
