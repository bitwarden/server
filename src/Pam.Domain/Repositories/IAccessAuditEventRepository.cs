using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessAuditEventRepository
{
    /// <summary>
    /// Returns the synthesized PAM access-audit trail for the given (caller-manageable) collections — every event
    /// occurring on or after <paramref name="since"/>, newest first. The events are projected from existing PAM entity
    /// state (<see cref="Entities.AccessRequest"/>, <see cref="Entities.AccessLease"/>,
    /// <see cref="Entities.AccessDecision"/>); nothing is persisted. <paramref name="now"/> dates the derived expiry
    /// events — an approved request whose window lapsed unused, and a lease already past its window. Returns an empty
    /// collection when no collection ids are supplied. Rule-administration events are not included (they scope through
    /// a different path).
    /// </summary>
    Task<ICollection<AccessAuditEvent>> GetManyByCollectionIdsAsync(
        IEnumerable<Guid> collectionIds, DateTime since, DateTime now);
}
