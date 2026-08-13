#nullable enable
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Repositories;

namespace Bit.Api.AdminConsole.Authorization.OrganizationUsers;

public class OrganizationUserAuthorizationService(
    ICurrentContext currentContext,
    IOrganizationUserRepository organizationUserRepository,
    ICollectionRepository collectionRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService) : IOrganizationUserAuthorizationService
{
    public async Task<OrganizationUserAuthorizationResult> AuthorizeUpdateAsync(
        Guid organizationId,
        Guid organizationUserId,
        IReadOnlyCollection<Guid> postedCollectionIds)
    {
        var (organizationUser, currentAccess) = await organizationUserRepository.GetByIdWithCollectionsAsync(organizationUserId);
        if (organizationUser is null || organizationUser.OrganizationId != organizationId)
        {
            return new OrganizationUserAuthorizationResult(false, false, new HashSet<Guid>(), new HashSet<Guid>());
        }

        if (!currentContext.UserId.HasValue)
        {
            return new OrganizationUserAuthorizationResult(false, false, new HashSet<Guid>(), new HashSet<Guid>());
        }

        var organization = currentContext.GetOrganization(organizationId);
        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        var allowAdminAccessToAllCollectionItems = organizationAbility is { AllowAdminAccessToAllCollectionItems: true };

        var editingSelf = currentContext.UserId == organizationUser.UserId;
        var currentAccessIds = currentAccess.Select(c => c.Id).ToHashSet();

        // A self-editing user can't add themselves to collections they don't already have access to,
        // unless admins can access all collections.
        var canAddSelfToCollection = !editingSelf
            || allowAdminAccessToAllCollectionItems
            || postedCollectionIds.All(currentAccessIds.Contains);

        // If admins are not allowed access to all collections, a self-editing user can't edit their own groups.
        var canEditOwnGroups = !editingSelf || allowAdminAccessToAllCollectionItems;

        var managedCollectionIds = await GetManagedCollectionIdsAsync(currentContext.UserId.Value);

        var unauthorizedPostedCollectionIds = await GetUnauthorizedCollectionIdsAsync(
            postedCollectionIds, organizationId, organization, allowAdminAccessToAllCollectionItems, managedCollectionIds);
        var readonlyCurrentCollectionIds = await GetUnauthorizedCollectionIdsAsync(
            currentAccessIds, organizationId, organization, allowAdminAccessToAllCollectionItems, managedCollectionIds);

        if ((!canAddSelfToCollection || unauthorizedPostedCollectionIds.Count > 0)
            && await currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return new OrganizationUserAuthorizationResult(true, canEditOwnGroups, new HashSet<Guid>(), new HashSet<Guid>());
        }

        return new OrganizationUserAuthorizationResult(canAddSelfToCollection, canEditOwnGroups, unauthorizedPostedCollectionIds, readonlyCurrentCollectionIds);
    }

    private async Task<HashSet<Guid>> GetManagedCollectionIdsAsync(Guid userId)
    {
        var callerCollections = await collectionRepository.GetManyByUserIdAsync(userId);
        return callerCollections.Where(c => c.Manage).Select(c => c.Id).ToHashSet();
    }

    private async Task<HashSet<Guid>> GetUnauthorizedCollectionIdsAsync(
        IEnumerable<Guid> collectionIds,
        Guid organizationId,
        CurrentContextOrganization? organization,
        bool allowAdminAccessToAllCollectionItems,
        HashSet<Guid> managedCollectionIds)
    {
        var unauthorized = new HashSet<Guid>();
        foreach (var collectionId in collectionIds)
        {
            var (collection, accessDetails) = await collectionRepository.GetByIdWithAccessAsync(collectionId);
            if (collection is null || collection.OrganizationId != organizationId)
            {
                continue;
            }

            var callerManagesCollection = managedCollectionIds.Contains(collectionId);
            if (!CollectionRules.CanModifyUserAccess(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection))
            {
                unauthorized.Add(collectionId);
            }
        }

        return unauthorized;
    }
}
