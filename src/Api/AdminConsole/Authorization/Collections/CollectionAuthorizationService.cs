#nullable enable
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Repositories;

namespace Bit.Api.AdminConsole.Authorization.Collections;

public class CollectionAuthorizationService(
    ICurrentContext currentContext,
    ICollectionRepository collectionRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService) : ICollectionAuthorizationService
{
    public async Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId)
    {
        var (collection, accessDetails) = await collectionRepository.GetByIdWithAccessAsync(collectionId);
        if (collection is null || collection.OrganizationId != organizationId)
        {
            return false;
        }

        if (!currentContext.UserId.HasValue)
        {
            return false;
        }

        var organization = currentContext.GetOrganization(organizationId);
        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        var allowAdminAccessToAllCollectionItems = organizationAbility is { AllowAdminAccessToAllCollectionItems: true };

        var callerManagesCollection = await CallerManagesCollectionAsync(currentContext.UserId.Value, collectionId);

        var canUpdate = CollectionRules.CanUpdate(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection);
        var canModifyUserAccess = CollectionRules.CanModifyUserAccess(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection);
        var canModifyGroupAccess = CollectionRules.CanModifyGroupAccess(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection);

        if (canUpdate && canModifyUserAccess && canModifyGroupAccess)
        {
            return true;
        }

        return await currentContext.ProviderUserForOrgAsync(organizationId);
    }

    private async Task<bool> CallerManagesCollectionAsync(Guid userId, Guid collectionId)
    {
        var callerCollections = await collectionRepository.GetManyByUserIdAsync(userId);
        return callerCollections.Any(c => c.Id == collectionId && c.Manage);
    }
}
