using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessAuditEventRepository
{
    /// <summary>
    /// Returns the synthesized PAM access-audit trail for an entire organization — every event occurring on or after
    /// <paramref name="since"/>, newest first. Events are projected from existing PAM entity state
    /// (<see cref="Entities.AccessRequest"/>, <see cref="Entities.AccessLease"/>, <see cref="Entities.AccessRule"/> and
    /// <see cref="Entities.AccessDecision"/>); nothing is persisted. <paramref name="now"/> dates the derived expiry
    /// events — an approved request whose window lapsed unused, and a lease already past its window. The trail is
    /// org-wide (the caller is authorized by the AccessEventLogs permission at the endpoint, not by collection
    /// management), so the access-request, access-lease, and rule-administration kinds are all included.
    /// </summary>
    Task<ICollection<AccessAuditEvent>> GetManyByOrganizationIdAsync(
        Guid organizationId, DateTime since, DateTime now);
}
