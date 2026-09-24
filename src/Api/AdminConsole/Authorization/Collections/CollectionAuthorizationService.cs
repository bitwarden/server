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
    // Collection ID mapped to its organization ID, or null if unresolved
    private readonly Dictionary<Guid, Guid?> _organizationIdByCollectionId = new();
    // Orphaned collection IDs per organization; only fetched for Owner/Admin callers
    private readonly Dictionary<Guid, HashSet<Guid>> _orphanedCollectionIdsByOrganizationId = new();
    // Collections the caller manages, across organizations; null until first fetched
    private HashSet<Guid>? _callerManagedCollectionIds;

    public async Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId) =>
        (await AuthorizeAsync(organizationId, [collectionId], CollectionRules.OrganizationRole.CanUpdate)).Contains(collectionId);

    public Task<IReadOnlySet<Guid>> AuthorizeModifyUserAccessAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds) =>
        AuthorizeAsync(organizationId, collectionIds, CollectionRules.OrganizationRole.CanModifyUserAccess);

    public Task<IReadOnlySet<Guid>> AuthorizeModifyGroupAccessAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds) =>
        AuthorizeAsync(organizationId, collectionIds, CollectionRules.OrganizationRole.CanModifyGroupAccess);

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
        var orphanedCollectionIds = hasUnmanagedCollections && CollectionRules.CollectionAssignment.CanManageOrphanedCollections(organization)
            ? await GetOrphanedCollectionIdsAsync(organizationId)
            : new HashSet<Guid>();

        var authorizedCollectionIds = requestedCollectionIds
            .Where(id => CollectionRules.CollectionAssignment.CanManage(
                organization,
                new CollectionRules.CollectionAssignment.ManagementFacts(
                    CallerManagesCollection: callerManagedCollectionIds.Contains(id),
                    IsOrphaned: orphanedCollectionIds.Contains(id))))
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
        await EnsureCollectionOrganizationsCachedAsync(collectionIds);

        bool BelongsToRequestedOrganization(Guid id) => _organizationIdByCollectionId[id] == organizationId;

        return collectionIds.Where(BelongsToRequestedOrganization).ToHashSet();
    }

    private async Task EnsureCollectionOrganizationsCachedAsync(IReadOnlyCollection<Guid> collectionIds)
    {
        var uncachedIds = collectionIds.Where(id => !_organizationIdByCollectionId.ContainsKey(id)).ToList();
        if (uncachedIds.Count == 0)
        {
            return;
        }

        var collections = await collectionRepository.GetManyByManyIdsAsync(uncachedIds);
        var organizationIdsByCollectionId = collections.ToDictionary(c => c.Id, c => (Guid?)c.OrganizationId);

        foreach (var id in uncachedIds)
        {
            _organizationIdByCollectionId[id] = organizationIdsByCollectionId.GetValueOrDefault(id);
        }
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
            .Where(result => CollectionRules.CollectionAssignment.IsOrphaned(result.Item2))
            .Select(result => result.Item1.Id)
            .ToHashSet();
        _orphanedCollectionIdsByOrganizationId[organizationId] = orphanedIds;

        return orphanedIds;
    }
}
