using Bit.Core.Enums;
using Bit.Core.Utilities;
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
        var collectionList = CreateAndSaveCollections(organizationId, collections);

        if (collectionList.Any())
        {
            CreateAndSaveCollectionUserRelationships(collectionList, organizationUserIds, maxUsersWithRelationships);
        }

        return collectionList.Select(c => c.Id).ToList();
    }

    private List<Core.Entities.Collection> CreateAndSaveCollections(Guid organizationId, int count)
    {
        var collectionList = new List<Core.Entities.Collection>();

        for (var i = 0; i < count; i++)
        {
            collectionList.Add(new Core.Entities.Collection
            {
                Id = CoreHelpers.GenerateComb(),
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
        }

        return collectionList;
    }

    private void CreateAndSaveCollectionUserRelationships(
        List<Core.Entities.Collection> collections,
        List<Guid> organizationUserIds,
        int maxUsersWithRelationships)
    {
        if (!organizationUserIds.Any() || maxUsersWithRelationships <= 0)
        {
            return;
        }

        var collectionUsers = BuildCollectionUserRelationships(collections, organizationUserIds, maxUsersWithRelationships);

        if (collectionUsers.Any())
        {
            db.BulkCopy(collectionUsers);
        }
    }

    /// <summary>
    /// Creates user-to-collection relationships with varied assignment patterns for realistic test data.
    /// Each user gets 1-3 collections based on a rotating pattern.
    /// </summary>
    private List<Core.Entities.CollectionUser> BuildCollectionUserRelationships(
        List<Core.Entities.Collection> collections,
        List<Guid> organizationUserIds,
        int maxUsersWithRelationships)
    {
        var maxRelationships = Math.Min(organizationUserIds.Count, maxUsersWithRelationships);
        var collectionUsers = new List<Core.Entities.CollectionUser>();

        for (var i = 0; i < maxRelationships; i++)
        {
            var orgUserId = organizationUserIds[i];
            var userCollectionAssignments = CreateCollectionAssignmentsForUser(collections, orgUserId, i);
            collectionUsers.AddRange(userCollectionAssignments);
        }

        return collectionUsers;
    }

    /// <summary>
    /// Assigns collections to a user with varying permissions.
    /// Pattern: 1-3 collections per user (cycles: 1, 2, 3, 1, 2, 3...).
    /// First collection has Manage rights, subsequent ones are ReadOnly.
    /// </summary>
    private List<Core.Entities.CollectionUser> CreateCollectionAssignmentsForUser(
        List<Core.Entities.Collection> collections,
        Guid organizationUserId,
        int userIndex)
    {
        var assignments = new List<Core.Entities.CollectionUser>();
        var userCollectionCount = (userIndex % 3) + 1; // Cycles through 1, 2, or 3 collections

        for (var j = 0; j < userCollectionCount; j++)
        {
            var collectionIndex = (userIndex + j) % collections.Count; // Distribute across available collections
            assignments.Add(new Core.Entities.CollectionUser
            {
                CollectionId = collections[collectionIndex].Id,
                OrganizationUserId = organizationUserId,
                ReadOnly = j > 0,      // First assignment gets write access
                HidePasswords = false,
                Manage = j == 0        // First assignment gets manage permissions
            });
        }

        return assignments;
    }
}
