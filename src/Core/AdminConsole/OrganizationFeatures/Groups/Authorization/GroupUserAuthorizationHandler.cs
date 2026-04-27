#nullable enable

using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

public class GroupUserAuthorizationHandler(
    ICurrentContext currentContext,
    IApplicationCacheService applicationCacheService,
    IOrganizationUserRepository organizationUserRepository,
    IGroupRepository groupRepository)
    : AuthorizationHandler<GroupUserOperationRequirement, GroupUserAssignmentContext>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GroupUserOperationRequirement requirement,
        GroupUserAssignmentContext resource)
    {
        var authorized = requirement.Name switch
        {
            nameof(GroupUserOperations.AssignUsers) => await CanAssignUsersAsync(resource),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanAssignUsersAsync(GroupUserAssignmentContext resource)
    {
        var orgAbility = await applicationCacheService.GetOrganizationAbilityAsync(resource.OrganizationId);

        // When admins have unrestricted collection access, self-assignment to groups is permitted.
        if (orgAbility.AllowAdminAccessToAllCollectionItems)
        {
            return true;
        }

        if (currentContext.UserId is null)
        {
            return false;
        }

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(
            resource.OrganizationId, currentContext.UserId.Value);

        // Providers are not org members and are exempt from this restriction.
        if (organizationUser is null)
        {
            return true;
        }

        // If the caller's own OrganizationUser ID is not among the requested users, there is no self-assignment.
        if (!resource.RequestedUserIds.Contains(organizationUser.Id))
        {
            return true;
        }

        // When a GroupId is provided, check whether the caller is already a member of the group.
        // Keeping an existing membership is permitted; only newly adding oneself is blocked.
        if (resource.GroupId.HasValue)
        {
            var currentGroupUsers = await groupRepository.GetManyUserIdsByIdAsync(resource.GroupId.Value);
            if (currentGroupUsers.Contains(organizationUser.Id))
            {
                return true;
            }
        }

        return false;
    }
}
