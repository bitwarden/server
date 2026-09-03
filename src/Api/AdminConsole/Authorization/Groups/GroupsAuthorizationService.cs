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
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    ICollectionAuthorizationService collectionAuthorizationService) : IGroupsAuthorizationService
{
    public async Task<GroupsAuthorizationResult> AuthorizeSaveAsync(
        Guid organizationId,
        Guid? groupId,
        IReadOnlyCollection<Guid> postedCollectionIds,
        IReadOnlyCollection<Guid> currentCollectionIds,
        IReadOnlyCollection<Guid> postedUserIds)
    {
        var authorizedPostedCollectionIds =
            await collectionAuthorizationService.AuthorizeModifyGroupAccessManyAsync(organizationId, postedCollectionIds);
        var authorizedCurrentCollectionIds =
            await collectionAuthorizationService.AuthorizeModifyGroupAccessManyAsync(organizationId, currentCollectionIds);

        return new GroupsAuthorizationResult(
            await CanAddSelfToGroupAsync(organizationId, groupId, postedUserIds),
            postedCollectionIds.Except(authorizedPostedCollectionIds).ToHashSet(),
            currentCollectionIds.Except(authorizedCurrentCollectionIds).ToHashSet());
    }

    private async Task<bool> CanAddSelfToGroupAsync(
        Guid organizationId,
        Guid? groupId,
        IReadOnlyCollection<Guid> postedUserIds)
    {
        // Creating a group has never had this rule. It is not watertight on update either: a caller can create an
        // empty group with themselves in it, then add collections through update as an existing member.
        if (groupId is null)
        {
            return true;
        }

        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (organizationAbility is { AllowAdminAccessToAllCollectionItems: true } || !currentContext.UserId.HasValue)
        {
            return true;
        }

        // A provider is not an organization member, so it has no organization user to add.
        var callerOrganizationUser =
            await organizationUserRepository.GetByOrganizationAsync(organizationId, currentContext.UserId.Value);
        if (callerOrganizationUser is null || !postedUserIds.Contains(callerOrganizationUser.Id))
        {
            return true;
        }

        var currentGroupUserIds = await groupRepository.GetManyUserIdsByIdAsync(groupId.Value);
        return currentGroupUserIds.Contains(callerOrganizationUser.Id);
    }
}
