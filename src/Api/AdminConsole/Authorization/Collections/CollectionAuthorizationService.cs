using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;

namespace Bit.Api.AdminConsole.Authorization.Collections;

public class CollectionAuthorizationService(
    ICurrentContext currentContext,
    ICollectionRepository collectionRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    IProviderRepository providerRepository) : ICollectionAuthorizationService
{
    // A null value means the collection was not found.
    private readonly Dictionary<Guid, CollectionAdminDetails?> _detailsByCollectionId = new();

    public async Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId) =>
        (await AuthorizeAsync(organizationId, [collectionId], CollectionRules.OrganizationRole.CanUpdate,
            excludeDefaultCollections: false)).Contains(collectionId);

    public Task<IReadOnlySet<Guid>> AuthorizeModifyUserAccessAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds) =>
        AuthorizeAsync(organizationId, collectionIds, CollectionRules.OrganizationRole.CanModifyUserAccess,
            excludeDefaultCollections: true);

    public Task<IReadOnlySet<Guid>> AuthorizeModifyGroupAccessAsync(Guid organizationId, IReadOnlyCollection<Guid> collectionIds) =>
        AuthorizeAsync(organizationId, collectionIds, CollectionRules.OrganizationRole.CanModifyGroupAccess,
            excludeDefaultCollections: true);

    /// <summary>
    /// Returns the subset of <paramref name="collectionIds"/> that the caller can operate on.
    /// </summary>
    private async Task<IReadOnlySet<Guid>> AuthorizeAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> collectionIds,
        Func<CurrentContextOrganization?, OrganizationAbility?, bool> organizationWideRule,
        bool excludeDefaultCollections)
    {
        if (collectionIds.Count == 0 || !currentContext.UserId.HasValue)
        {
            return new HashSet<Guid>();
        }

        await EnsureCollectionDetailsCachedAsync(collectionIds);

        var requestedCollectionIds = collectionIds
            .Where(id => _detailsByCollectionId[id] is { } details
                         && details.OrganizationId == organizationId
                         && (!excludeDefaultCollections || details.Type != CollectionType.DefaultUserCollection))
            .ToHashSet();
        if (requestedCollectionIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var organization = currentContext.GetOrganization(organizationId);
        if (organization is null)
        {
            // A non-member can only be authorized as a provider user.
            return await IsManagingProviderUserAsync(organizationId)
                ? requestedCollectionIds
                : [];
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
            await IsManagingProviderUserAsync(organizationId))
        {
            return requestedCollectionIds;
        }

        return authorizedCollectionIds;
    }

    /// <summary>
    /// Returns true if the caller is a user of the organization's provider and that provider can manage collections.
    /// </summary>
    private async Task<bool> IsManagingProviderUserAsync(Guid organizationId) =>
        await currentContext.ProviderUserForOrgAsync(organizationId) &&
        CollectionRules.ManagingProvider.CanManageCollections(
            await providerRepository.GetByOrganizationIdAsync(organizationId));

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
