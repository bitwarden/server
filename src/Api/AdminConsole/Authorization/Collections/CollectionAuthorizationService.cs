using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Models.Data;
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

        // Check if the caller can update the collection without directly managing it.
        // Checking direct access is deferred because it's more expensive to check.
        if (IsAuthorized(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection: false))
        {
            return true;
        }

        var callerManagesCollection = await CallerManagesCollectionAsync(currentContext.UserId.Value, collectionId);
        if (IsAuthorized(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection))
        {
            return true;
        }

        return await currentContext.ProviderUserForOrgAsync(organizationId);
    }

    private static bool IsAuthorized(
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization? organization,
        bool allowAdminAccessToAllCollectionItems,
        bool callerManagesCollection)
    {
        return CollectionRules.CanUpdate(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection)
            && CollectionRules.CanModifyUserAccess(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection)
            && CollectionRules.CanModifyGroupAccess(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection);
    }

    private async Task<bool> CallerManagesCollectionAsync(Guid userId, Guid collectionId)
    {
        var callerCollections = await collectionRepository.GetManyByUserIdAsync(userId);
        return callerCollections.Any(c => c.Id == collectionId && c.Manage);
    }
}
