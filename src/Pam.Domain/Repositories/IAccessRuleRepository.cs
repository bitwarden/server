using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessRuleRepository : IRepository<AccessRule, Guid>
{
    Task<ICollection<AccessRule>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the access rule along with the IDs of the collections it governs, or null if it does not exist.
    /// </summary>
    Task<AccessRuleDetails?> GetDetailsByIdAsync(Guid id);

    /// <summary>
    /// Returns all access rules in the organization, each along with the IDs of the collections it governs.
    /// </summary>
    Task<ICollection<AccessRuleDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Points the given collections at the access rule and clears the rule from any collections that should no
    /// longer reference it. Both sets are scoped to the organization.
    /// </summary>
    /// <param name="organizationId">The organization that owns the access rule and collections.</param>
    /// <param name="accessRuleId">The access rule to associate.</param>
    /// <param name="collectionIdsToAssign">Collections that should reference the access rule.</param>
    /// <param name="collectionIdsToClear">Collections whose reference to the access rule should be removed.</param>
    Task SetCollectionAssociationsAsync(Guid organizationId, Guid accessRuleId,
        IEnumerable<Guid> collectionIdsToAssign, IEnumerable<Guid> collectionIdsToClear);

    /// <summary>
    /// Soft-deletes the rule: stamps DeletedDate/DeletedBy so it stops governing access (every gating read excludes
    /// deleted rules) while preserving its collection links for the audit trail. Idempotent for an already-deleted rule.
    /// </summary>
    Task SoftDeleteAsync(Guid id, Guid? deletedBy, DateTime deletedDate);
}
