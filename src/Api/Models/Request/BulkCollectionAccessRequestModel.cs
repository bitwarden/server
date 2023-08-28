

using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Api.Models.Request;

public class BulkCollectionAccessRequestModel
{
    public IEnumerable<Guid> CollectionIds { get; set; }

    public IEnumerable<SelectionReadOnlyRequestModel> Groups { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Users { get; set; }

    /// <summary>
    /// Build a list of <see cref="ICollectionAccess"/> entities from combinations of every
    /// <see cref="CollectionIds"/> and <see cref="Users"/>/<see cref="Groups"/>.
    /// </summary>
    public IEnumerable<ICollectionAccess> ToCollectionAccessList()
    {
        if (CollectionIds == null || !CollectionIds.Any())
        {
            throw new BadRequestException("No collections were provided.");
        }

        var collectionAccess = new List<ICollectionAccess>();

        if (Users != null)
        {
            collectionAccess.AddRange(
                CollectionIds.SelectMany(collectionId =>
                    Users.Select(u => new CollectionUser
                    {
                        CollectionId = collectionId,
                        OrganizationUserId = u.Id,
                        Manage = u.Manage,
                        HidePasswords = u.HidePasswords,
                        ReadOnly = u.ReadOnly
                    })
                )
            );
        }

        if (Groups != null)
        {
            collectionAccess.AddRange(
                CollectionIds.SelectMany(collectionId =>
                    Groups.Select(u => new CollectionGroup
                    {
                        CollectionId = collectionId,
                        GroupId = u.Id,
                        Manage = u.Manage,
                        HidePasswords = u.HidePasswords,
                        ReadOnly = u.ReadOnly
                    })
                )
            );
        }

        if (!collectionAccess.Any())
        {
            throw new BadRequestException("No users or groups were provided.");
        }

        return collectionAccess;
    }
}
