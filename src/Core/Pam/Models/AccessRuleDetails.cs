using Bit.Core.Pam.Entities;

namespace Bit.Core.Pam.Models;

/// <summary>
/// An <see cref="AccessRule"/> together with the IDs of the collections it governs.
/// </summary>
public class AccessRuleDetails : AccessRule
{
    public IEnumerable<Guid> CollectionIds { get; set; } = [];

    public static AccessRuleDetails From(AccessRule rule, IEnumerable<Guid> collectionIds) => new()
    {
        Id = rule.Id,
        OrganizationId = rule.OrganizationId,
        Name = rule.Name,
        Description = rule.Description,
        Conditions = rule.Conditions,
        CreationDate = rule.CreationDate,
        RevisionDate = rule.RevisionDate,
        CollectionIds = collectionIds,
    };
}
