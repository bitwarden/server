using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListAccessAuditTrailQuery
{
    /// <summary>
    /// Returns the org-wide access-audit trail for <paramref name="organizationId"/>, within the shared history
    /// window, newest first — every access-request, access-lease, and rule-administration event in the organization.
    /// Authorization (the AccessEventLogs permission) is enforced at the endpoint before this runs. Events are read
    /// from the dedicated append-only audit store, where each was written (self-contained) at the moment it happened.
    /// </summary>
    Task<ICollection<AccessAuditEvent>> GetTrailAsync(Guid organizationId);
}
