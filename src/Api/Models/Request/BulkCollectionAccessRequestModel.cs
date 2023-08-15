

using Bit.Core.Entities;

namespace Bit.Api.Models.Request;

public class BulkCollectionAccessRequestModel
{
    public IEnumerable<Guid> CollectionIds { get; set; }

    public IEnumerable<SelectionReadOnlyRequestModel> Groups { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Users { get; set; }

    /// <summary>
    /// Build a list of <see cref="CollectionUser"/> entities from combinations of every
    /// <see cref="CollectionIds"/> and <see cref="Users"/>.
    /// </summary>
    public IEnumerable<CollectionUser> ToAllCollectionUsers()
    {
        return CollectionIds.SelectMany(collectionId => Users.Select(u => new CollectionUser
        {
            CollectionId = collectionId,
            OrganizationUserId = u.Id,
            Manage = u.Manage,
            HidePasswords = u.HidePasswords,
            ReadOnly = u.ReadOnly
        }));
    }

    /// <summary>
    /// Build a list of <see cref="CollectionGroup"/> entities from combinations of every
    /// <see cref="CollectionIds"/> and <see cref="Groups"/>.
    /// </summary>
    public IEnumerable<CollectionGroup> ToAllCollectionGroups()
    {
        return CollectionIds.SelectMany(collectionId => Groups.Select(u => new CollectionGroup
        {
            CollectionId = collectionId,
            GroupId = u.Id,
            Manage = u.Manage,
            HidePasswords = u.HidePasswords,
            ReadOnly = u.ReadOnly
        }));
    }
}
