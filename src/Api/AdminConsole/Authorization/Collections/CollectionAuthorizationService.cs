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
    public Task<bool> AuthorizeUpdateAsync(Guid organizationId, Guid collectionId) =>
        AuthorizeAsync(organizationId, collectionId, CollectionRules.CanUpdate);

    public Task<bool> AuthorizeModifyUserAccessAsync(Guid organizationId, Guid collectionId) =>
        AuthorizeAsync(organizationId, collectionId, CollectionRules.CanModifyUserAccess);

    public Task<bool> AuthorizeModifyGroupAccessAsync(Guid organizationId, Guid collectionId) =>
        AuthorizeAsync(organizationId, collectionId, CollectionRules.CanModifyGroupAccess);

    private async Task<bool> AuthorizeAsync(
        Guid organizationId,
        Guid collectionId,
        Func<CollectionAccessDetails, CurrentContextOrganization?, bool, bool, bool> isAuthorized)
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

        // Check if the caller is authorized without directly managing the collection.
        // Checking direct access is deferred because it's more expensive to check.
        if (isAuthorized(accessDetails, organization, allowAdminAccessToAllCollectionItems, false))
        {
            return true;
        }

        var callerManagesCollection = await CallerManagesCollectionAsync(currentContext.UserId.Value, collectionId);
        if (isAuthorized(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection))
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
