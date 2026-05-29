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
    /// <returns>A tuple containing the collections and collection users.</returns>
    public static (ICollection<Collection> collections, ICollection<CollectionUser> collectionUsers)
        BuildDefaultUserCollections(Guid organizationId, IEnumerable<Guid> organizationUserIds,
            string defaultCollectionName)
    {
        var now = DateTime.UtcNow;

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
                CreationDate = now,
                RevisionDate = now,
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
