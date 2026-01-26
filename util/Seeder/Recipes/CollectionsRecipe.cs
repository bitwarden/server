using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Data;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Factories;
using LinqToDB.EntityFrameworkCore;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Creates collections for seeding organization vaults.
/// </summary>
public class CollectionsRecipe(DatabaseContext db, RustSdkService? sdkService = null)
{
    private readonly CollectionSeeder? _collectionSeeder = sdkService != null ? new(sdkService) : null;

    /// <summary>
    /// Creates collections from an organizational structure (e.g., Traditional departments, Spotify tribes).
    /// Collection names are properly encrypted.
    /// </summary>
    public List<Guid> AddFromStructure(
        Guid organizationId,
        string orgKeyBase64,
        OrgStructureModel model,
        List<Guid> organizationUserIds,
        int maxUsersWithRelationships = 1000)
    {
        var structure = OrgStructures.GetStructure(model);

        var collections = structure.Units
            .Select(unit => _collectionSeeder!.CreateCollection(organizationId, orgKeyBase64, unit.Name))
            .ToList();

        db.BulkCopy(collections);

        if (collections.Count > 0 && organizationUserIds.Count > 0 && maxUsersWithRelationships > 0)
        {
            var collectionUsers = BuildCollectionUserRelationships(collections, organizationUserIds, maxUsersWithRelationships);
            db.BulkCopy(collectionUsers);
        }

        return collections.Select(c => c.Id).ToList();
    }

    /// <summary>
    /// Adds generic numbered collections (unencrypted names - use AddFromStructure for realistic data).
    /// </summary>
    public List<Guid> AddToOrganization(Guid organizationId, int collections, List<Guid> organizationUserIds, int maxUsersWithRelationships = 1000)
    {
        var collectionList = Enumerable.Range(0, collections)
            .Select(i => new Core.Entities.Collection
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = organizationId,
                Name = $"Collection {i + 1}",
                Type = CollectionType.SharedCollection,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            })
            .ToList();

        db.BulkCopy(collectionList);

        if (collectionList.Count > 0 && organizationUserIds.Count > 0 && maxUsersWithRelationships > 0)
        {
            var collectionUsers = BuildCollectionUserRelationships(collectionList, organizationUserIds, maxUsersWithRelationships);
            db.BulkCopy(collectionUsers);
        }

        return collectionList.Select(c => c.Id).ToList();
    }

    /// <summary>
    /// Creates user-to-collection relationships with varied assignment patterns.
    /// Each user gets 1-3 collections (cycling). First collection has Manage rights.
    /// </summary>
    private static List<Core.Entities.CollectionUser> BuildCollectionUserRelationships(
        List<Core.Entities.Collection> collections,
        List<Guid> organizationUserIds,
        int maxUsersWithRelationships)
    {
        return organizationUserIds
            .Take(maxUsersWithRelationships)
            .SelectMany((orgUserId, userIndex) =>
            {
                var collectionCount = (userIndex % 3) + 1; // Cycles through 1, 2, or 3
                return Enumerable.Range(0, collectionCount)
                    .Select(j => new Core.Entities.CollectionUser
                    {
                        CollectionId = collections[(userIndex + j) % collections.Count].Id,
                        OrganizationUserId = orgUserId,
                        ReadOnly = j > 0,
                        HidePasswords = false,
                        Manage = j == 0
                    });
            })
            .ToList();
    }
}
