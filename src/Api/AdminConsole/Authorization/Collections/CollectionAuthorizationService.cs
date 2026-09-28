using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;

namespace Bit.Api.AdminConsole.Authorization.Collections;

public class CollectionAuthorizationService(
    ICurrentContext currentContext,
    ICollectionRepository collectionRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService) : ICollectionAuthorizationService
{
    // Collection ID and its details, or null if the collection was not found.
    private readonly Dictionary<Guid, CollectionAdminDetails?> _detailsByCollectionId = new();

    public async Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId) =>
        (await AuthorizeAsync(organizationId, [collectionId], CollectionRules.OrganizationRole.CanUpdate)).Contains(collectionId);

    public Task<IReadOnlySet<Guid>> AuthorizeModifyUserAccessAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds) =>
        AuthorizeAsync(organizationId, collectionIds, CollectionRules.OrganizationRole.CanModifyUserAccess);

    public Task<IReadOnlySet<Guid>> AuthorizeModifyGroupAccessAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds) =>
        AuthorizeAsync(organizationId, collectionIds, CollectionRules.OrganizationRole.CanModifyGroupAccess);

    /// <summary>
    /// Returns the subset of <paramref name="collectionIds"/> that the caller is authorized to operate on.
    /// Organization-wide rules are applied first. If none apply to the caller then each collection is
    /// checked on its own.
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

        await EnsureCollectionDetailsCachedAsync(collectionIds);

        var requestedCollectionIds = collectionIds
            .Where(id => _detailsByCollectionId[id] is { } details && details.OrganizationId == organizationId)
            .ToHashSet();
        if (requestedCollectionIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var organization = currentContext.GetOrganization(organizationId);
        if (organization is null)
        {
            // A non-member has no organization permissions, so only a provider user can be authorized.
            return await currentContext.ProviderUserForOrgAsync(organizationId)
                ? requestedCollectionIds
                : new HashSet<Guid>();
        }

        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (organizationWideRule(organization, organizationAbility))
        {
            return requestedCollectionIds;
        }

        var authorizedCollectionIds = requestedCollectionIds
            .Where(id => CollectionRules.CollectionAssignment.CanManage(organization, _detailsByCollectionId[id]!))
            .ToHashSet();

        if (authorizedCollectionIds.Count < requestedCollectionIds.Count &&
            await currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return requestedCollectionIds;
        }

        return authorizedCollectionIds;
    }

    private async Task EnsureCollectionDetailsCachedAsync(IReadOnlyCollection<Guid> collectionIds)
    {
        var uncachedIds = collectionIds.Where(id => !_detailsByCollectionId.ContainsKey(id)).ToList();
        if (uncachedIds.Count == 0)
        {
            return;
        }

        var collections = await collectionRepository.GetManyByIdsWithPermissionsAsync(
            uncachedIds, currentContext.UserId, includeAccessRelationships: false);
        var detailsById = collections.ToDictionary(c => c.Id);

        foreach (var id in uncachedIds)
        {
            _detailsByCollectionId[id] = detailsById.GetValueOrDefault(id);
        }
    }
}
