#nullable enable
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Repositories;

namespace Bit.Api.AdminConsole.Authorization.Groups;

public class GroupsAuthorizationService(
    ICurrentContext currentContext,
    IGroupRepository groupRepository,
    IOrganizationUserRepository organizationUserRepository,
    ICollectionRepository collectionRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService) : IGroupsAuthorizationService
{
    public async Task<GroupsAuthorizationResult> AuthorizeUpdateAsync(
        Guid organizationId,
        Guid groupId,
        IReadOnlyCollection<Guid> postedCollectionIds,
        IReadOnlyCollection<Guid> postedUserIds)
    {
        var (group, currentAccess) = await groupRepository.GetByIdWithCollectionsAsync(groupId);
        if (group is null || group.OrganizationId != organizationId)
        {
            return new GroupsAuthorizationResult(false, new HashSet<Guid>(), new HashSet<Guid>());
        }

        if (!currentContext.UserId.HasValue)
        {
            return new GroupsAuthorizationResult(false, new HashSet<Guid>(), new HashSet<Guid>());
        }

        var organization = currentContext.GetOrganization(organizationId);
        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        var allowAdminAccessToAllCollectionItems = organizationAbility is { AllowAdminAccessToAllCollectionItems: true };

        var canAddSelfToGroup = await CanAddSelfToGroupAsync(organizationId, groupId, postedUserIds, allowAdminAccessToAllCollectionItems);

        var managedCollectionIds = await GetManagedCollectionIdsAsync(currentContext.UserId.Value);

        var unauthorizedPostedCollectionIds = await GetUnauthorizedCollectionIdsAsync(
            postedCollectionIds, organizationId, organization, allowAdminAccessToAllCollectionItems, managedCollectionIds);
        var readonlyCurrentCollectionIds = await GetUnauthorizedCollectionIdsAsync(
            currentAccess.Select(ca => ca.Id), organizationId, organization, allowAdminAccessToAllCollectionItems, managedCollectionIds);

        if ((!canAddSelfToGroup || unauthorizedPostedCollectionIds.Count > 0)
            && await currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return new GroupsAuthorizationResult(true, new HashSet<Guid>(), new HashSet<Guid>());
        }

        return new GroupsAuthorizationResult(canAddSelfToGroup, unauthorizedPostedCollectionIds, readonlyCurrentCollectionIds);
    }

    private async Task<bool> CanAddSelfToGroupAsync(
        Guid organizationId, Guid groupId, IReadOnlyCollection<Guid> postedUserIds, bool allowAdminAccessToAllCollectionItems)
    {
        if (allowAdminAccessToAllCollectionItems)
        {
            return true;
        }

        // The caller may be a provider rather than an organization member, in which case there's no self to add.
        var callerOrganizationUser = await organizationUserRepository.GetByOrganizationAsync(organizationId, currentContext.UserId!.Value);
        if (callerOrganizationUser is null)
        {
            return true;
        }

        var currentGroupUserIds = await groupRepository.GetManyUserIdsByIdAsync(groupId);
        return currentGroupUserIds.Contains(callerOrganizationUser.Id) || !postedUserIds.Contains(callerOrganizationUser.Id);
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
            if (!CollectionRules.CanModifyGroupAccess(accessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection))
            {
                unauthorized.Add(collectionId);
            }
        }

        return unauthorized;
    }
}
