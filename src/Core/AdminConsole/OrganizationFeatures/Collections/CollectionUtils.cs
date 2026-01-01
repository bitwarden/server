using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections;

public static class CollectionUtils
{
    /// <summary>
    /// Arranges Collection and CollectionUser objects to create default user collections.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="organizationUserIds">The IDs for organization users who need default collections.</param>
    /// <param name="defaultCollectionName">The encrypted string to use as the default collection name.</param>
    /// <returns></returns>
    public static (IEnumerable<Collection> collection, IEnumerable<CollectionUser> collectionUsers)
        BuildDefaultUserCollections(Guid organizationId, IEnumerable<Guid> organizationUserIds,
            string defaultCollectionName)
    {
        var collectionUsers = new List<CollectionUser>();
        var collections = new List<Collection>();

        foreach (var orgUserId in organizationUserIds)
        {
            var collectionId = CoreHelpers.GenerateComb();

            collections.Add(new Collection
            {
                Id = collectionId,
                OrganizationId = organizationId,
                Name = defaultCollectionName,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
                Type = CollectionType.DefaultUserCollection,
                DefaultUserCollectionEmail = null

            });

            collectionUsers.Add(new CollectionUser
            {
                CollectionId = collectionId,
                OrganizationUserId = orgUserId,
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
            });
        }

        return (collections, collectionUsers);
    }
}
