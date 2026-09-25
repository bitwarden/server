using Bit.Pam.Entities;

namespace Bit.Pam.Models;

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
        SingleActiveLease = rule.SingleActiveLease,
        DefaultLeaseDurationSeconds = rule.DefaultLeaseDurationSeconds,
        MaxLeaseDurationSeconds = rule.MaxLeaseDurationSeconds,
        Enabled = rule.Enabled,
        AllowsExtensions = rule.AllowsExtensions,
        MaxExtensionDurationSeconds = rule.MaxExtensionDurationSeconds,
        CreationDate = rule.CreationDate,
        RevisionDate = rule.RevisionDate,
        LastEditedBy = rule.LastEditedBy,
        CollectionIds = collectionIds,
    };
}
