using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB.EntityFrameworkCore;

namespace Bit.Seeder.Recipes;

public class CollectionsRecipe(DatabaseContext db)
{
    /// <summary>
    /// Adds collections to an organization and creates relationships between users and collections.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to add collections to.</param>
    /// <param name="collections">The number of collections to add.</param>
    /// <param name="organizationUserIds">The IDs of the users to create relationships with.</param>
    /// <param name="maxUsersWithRelationships">The maximum number of users to create relationships with.</param>
    public List<Guid> AddToOrganization(Guid organizationId, int collections, List<Guid> organizationUserIds, int maxUsersWithRelationships = 1000)
    {
        var collectionList = new List<Core.Entities.Collection>();
        for (var i = 0; i < collections; i++)
        {
            collectionList.Add(new Core.Entities.Collection
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = $"Collection {i + 1}",
                Type = CollectionType.SharedCollection,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            });
        }

        if (collectionList.Any())
        {
            db.BulkCopy(collectionList);

            if (organizationUserIds.Any() && maxUsersWithRelationships > 0)
            {
                var maxRelationships = Math.Min(organizationUserIds.Count, maxUsersWithRelationships);
                var collectionUsers = new List<Core.Entities.CollectionUser>();

                for (var i = 0; i < maxRelationships; i++)
                {
                    var orgUserId = organizationUserIds[i];

                    var userCollectionCount = (i % 3) + 1; // 1-3 collections per user
                    for (var j = 0; j < userCollectionCount; j++)
                    {
                        var collectionIndex = (i + j) % collectionList.Count;
                        collectionUsers.Add(new Core.Entities.CollectionUser
                        {
                            CollectionId = collectionList[collectionIndex].Id,
                            OrganizationUserId = orgUserId,
                            ReadOnly = j > 0,
                            HidePasswords = false,
                            Manage = j == 0
                        });
                    }
                }

                if (collectionUsers.Any())
                {
                    db.BulkCopy(collectionUsers);
                }
            }
        }

        return collectionList.Select(c => c.Id).ToList();
    }
}
