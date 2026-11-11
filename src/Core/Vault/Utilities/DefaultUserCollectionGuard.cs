using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Vault.Utilities;

/// <summary>
/// Prevents items from being assigned to another member's default ("My Items") collection.
/// A default collection is personal to its owner, even for callers who can edit all items in the organization.
/// </summary>
public static class DefaultUserCollectionGuard
{
    // Mirrors the generic permission error used elsewhere in CipherService so the response does not reveal that
    // the requested collection exists or is another member's default collection.
    private const string ForeignDefaultCollectionMessage = "You do not have permissions to edit this.";

    /// <summary>
    /// Throws a <see cref="BadRequestException"/> if any of <paramref name="collectionIds"/> is a default collection
    /// in <paramref name="organizationId"/> that is not owned by <paramref name="userId"/>.
    /// </summary>
    public static async Task EnsureNoForeignDefaultCollectionsAsync(
        ICollectionRepository collectionRepository,
        Guid organizationId,
        IEnumerable<Guid> collectionIds,
        Guid userId)
    {
        var collections = await collectionRepository.GetManyByManyIdsAsync(collectionIds);

        // Collections outside the organization are ignored so the error cannot reveal anything about another organization.
        var defaultCollectionIds = collections
            .Where(c => c.OrganizationId == organizationId && c.Type == CollectionType.DefaultUserCollection)
            .Select(c => c.Id)
            .ToList();

        if (defaultCollectionIds.Count == 0)
        {
            return;
        }

        var ownedDefaultCollectionIds = await GetOwnedDefaultCollectionIdsAsync(collectionRepository, userId);
        if (defaultCollectionIds.Any(id => !ownedDefaultCollectionIds.Contains(id)))
        {
            throw new BadRequestException(ForeignDefaultCollectionMessage);
        }
    }

    /// <summary>
    /// Returns the Ids of the default collections owned by <paramref name="userId"/>.
    /// </summary>
    public static async Task<HashSet<Guid>> GetOwnedDefaultCollectionIdsAsync(
        ICollectionRepository collectionRepository,
        Guid userId)
    {
        return (await collectionRepository.GetManyByUserIdAsync(userId))
            .Where(c => c.Type == CollectionType.DefaultUserCollection)
            .Select(c => c.Id)
            .ToHashSet();
    }
}
