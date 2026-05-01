using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Builds and caches the <see cref="CollectionAccessContext"/> for use across collection authorization handlers.
/// </summary>
public sealed class CollectionAccessContextService
{
    private readonly Dictionary<Guid, CollectionAccessContext> _cache = [];

    public async Task<CollectionAccessContext> GetOrBuildAsync(
        Guid organizationId,
        CurrentContextOrganization? organization,
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IApplicationCacheService applicationCacheService)
    {
        if (!_cache.TryGetValue(organizationId, out var context))
        {
            context = await BuildAsync(organizationId, organization, currentContext, collectionRepository, applicationCacheService);
            _cache[organizationId] = context;
        }

        return context;
    }

    private static async Task<CollectionAccessContext> BuildAsync(
        Guid organizationId,
        CurrentContextOrganization? organization,
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IApplicationCacheService applicationCacheService)
    {
        var ability = organization != null
            ? await applicationCacheService.GetOrganizationAbilityAsync(organization.Id)
            : null;

        var allowAdminAccess = ability is { AllowAdminAccessToAllCollectionItems: true };
        var isProviderUser = await currentContext.ProviderUserForOrgAsync(organizationId);

        var allUserCollections = await collectionRepository
            .GetManyByUserIdAsync(currentContext.UserId!.Value);

        var managedCollectionIds = allUserCollections
            .Where(c => c.Manage)
            .Select(c => c.Id)
            .ToHashSet();

        HashSet<Guid> orphanedCollectionIds;
        if (organization is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin }
            or { Permissions.EditAnyCollection: true })
        {
            var orgCollections = await collectionRepository
                .GetManyByOrganizationIdWithAccessAsync(organizationId);
            orphanedCollectionIds = orgCollections
                .Where(c => !c.Item2.Users.Any(u => u.Manage) && !c.Item2.Groups.Any(g => g.Manage))
                .Select(c => c.Item1.Id)
                .ToHashSet();
        }
        else
        {
            orphanedCollectionIds = [];
        }

        return new CollectionAccessContext(
            AllowAdminAccessToAllCollectionItems: allowAdminAccess,
            CallerIsProviderUser: isProviderUser,
            CallerManagedCollectionIds: managedCollectionIds,
            OrphanedCollectionIds: orphanedCollectionIds);
    }
}
