using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Builds the shared <see cref="CollectionAccessAuthorizationContext"/> for collection access handlers.
/// </summary>
internal static class CollectionAccessContextFactory
{
    public static async Task<CollectionAccessAuthorizationContext> BuildAsync(
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

        return new CollectionAccessAuthorizationContext(
            AllowAdminAccessToAllCollectionItems: allowAdminAccess,
            CallerIsProviderUser: isProviderUser,
            CallerManagedCollectionIds: managedCollectionIds,
            OrphanedCollectionIds: orphanedCollectionIds);
    }
}
