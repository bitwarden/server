using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;

namespace Bit.Api.AdminConsole.Authorization.Collections;

public class CollectionAuthorizationService(
    ICurrentContext currentContext,
    ICollectionRepository collectionRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService) : ICollectionAuthorizationService
{
    private readonly Dictionary<Guid, Guid?> _organizationIdByCollectionId = new();
    private readonly Dictionary<Guid, HashSet<Guid>> _orphanedCollectionIdsByOrganizationId = new();
    private HashSet<Guid>? _callerManagedCollectionIds;

    public async Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId) =>
        (await AuthorizeAsync(organizationId, [collectionId], CollectionRules.OrganizationWide.CanUpdate)).Contains(collectionId);

    public Task<IReadOnlySet<Guid>> AuthorizeModifyUserAccessManyAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds) =>
        AuthorizeAsync(organizationId, collectionIds, CollectionRules.OrganizationWide.CanModifyUserAccess);

    public Task<IReadOnlySet<Guid>> AuthorizeModifyGroupAccessManyAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds) =>
        AuthorizeAsync(organizationId, collectionIds, CollectionRules.OrganizationWide.CanModifyGroupAccess);

    /// <summary>
    /// Returns the subset of <paramref name="collectionIds"/> that the caller is authorized to operate on.
    /// The organization-wide rule is applied first. If it does not authorize the caller, each collection is then
    /// checked on its own. Data is read from the database only when it is needed, and each read is cached for the
    /// lifetime of the request.
    /// </summary>
    private async Task<IReadOnlySet<Guid>> AuthorizeAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> collectionIds,
        Func<CurrentContextOrganization?, OrganizationAbility?, bool> organizationWideRule)
    {
        if (collectionIds.Count == 0 || !currentContext.UserId.HasValue)
        {
            return new HashSet<Guid>();
        }

        var requestedCollectionIds = await GetCollectionIdsInOrganizationAsync(organizationId, collectionIds);
        if (requestedCollectionIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var organization = currentContext.GetOrganization(organizationId);
        if (organization is null)
        {
            // A non-member has no organization permissions for the rules to read, so only a provider user
            // can be authorized here.
            return await currentContext.ProviderUserForOrgAsync(organizationId)
                ? requestedCollectionIds
                : new HashSet<Guid>();
        }

        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (organizationWideRule(organization, organizationAbility))
        {
            return requestedCollectionIds;
        }

        var callerManagedCollectionIds = await GetCallerManagedCollectionIdsAsync(currentContext.UserId.Value);
        var hasUnmanagedCollections = requestedCollectionIds.Any(id => !callerManagedCollectionIds.Contains(id));
        // Only Owners and Admins can manage orphaned collections, and only unmanaged collections need the check.
        var orphanedCollectionIds = hasUnmanagedCollections && CollectionRules.PerCollection.CanManageOrphanedCollections(organization)
            ? await GetOrphanedCollectionIdsAsync(organizationId)
            : new HashSet<Guid>();

        var authorizedCollectionIds = requestedCollectionIds
            .Where(id => CollectionRules.PerCollection.CanManage(
                organization,
                callerManagesCollection: callerManagedCollectionIds.Contains(id),
                isCollectionOrphaned: orphanedCollectionIds.Contains(id)))
            .ToHashSet();

        if (authorizedCollectionIds.Count < requestedCollectionIds.Count &&
            await currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return requestedCollectionIds;
        }

        return authorizedCollectionIds;
    }

    private async Task<HashSet<Guid>> GetCollectionIdsInOrganizationAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> collectionIds)
    {
        var unresolvedIds = collectionIds.Where(id => !_organizationIdByCollectionId.ContainsKey(id)).ToList();
        if (unresolvedIds.Count != 0)
        {
            var collections = await collectionRepository.GetManyByManyIdsAsync(unresolvedIds);
            foreach (var id in unresolvedIds)
            {
                _organizationIdByCollectionId[id] = null;
            }

            foreach (var collection in collections)
            {
                _organizationIdByCollectionId[collection.Id] = collection.OrganizationId;
            }
        }

        return collectionIds.Where(id => _organizationIdByCollectionId[id] == organizationId).ToHashSet();
    }

    private async Task<HashSet<Guid>> GetCallerManagedCollectionIdsAsync(Guid userId)
    {
        if (_callerManagedCollectionIds is not null)
        {
            return _callerManagedCollectionIds;
        }

        var callerCollections = await collectionRepository.GetManyByUserIdAsync(userId);
        _callerManagedCollectionIds = callerCollections
            .Where(collection => collection.Manage)
            .Select(collection => collection.Id)
            .ToHashSet();

        return _callerManagedCollectionIds;
    }

    private async Task<HashSet<Guid>> GetOrphanedCollectionIdsAsync(Guid organizationId)
    {
        if (_orphanedCollectionIdsByOrganizationId.TryGetValue(organizationId, out var cachedIds))
        {
            return cachedIds;
        }

        var organizationCollections = await collectionRepository.GetManyByOrganizationIdWithAccessAsync(organizationId);
        var orphanedIds = organizationCollections
            .Where(result => CollectionRules.PerCollection.IsOrphaned(result.Item2))
            .Select(result => result.Item1.Id)
            .ToHashSet();
        _orphanedCollectionIdsByOrganizationId[organizationId] = orphanedIds;

        return orphanedIds;
    }
}
