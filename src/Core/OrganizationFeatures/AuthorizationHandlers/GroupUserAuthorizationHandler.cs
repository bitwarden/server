using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

class GroupUserAuthorizationHandler : AuthorizationHandler<GroupUserOperationRequirement, GroupUser>
{
    private readonly ICurrentContext _currentContext;
    private readonly IGroupRepository _groupRepository;

    public GroupUserAuthorizationHandler(ICurrentContext currentContext, IGroupRepository groupRepository)
    {
        _currentContext = currentContext;
        _groupRepository = groupRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupUserOperationRequirement requirement,
        GroupUser resource)
    {
        switch (requirement)
        {
            case not null when requirement == GroupUserOperations.Create:
                await CanCreateAsync(context, requirement, resource);
                break;

            case not null when requirement == GroupUserOperations.Delete:
                await CanDeleteAsync(context, requirement, resource);
                break;
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, GroupUserOperationRequirement requirement,
        GroupUser resource)
    {
        await CanManageAsync(context, requirement, resource);
    }

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, GroupUserOperationRequirement requirement,
        GroupUser resource)
    {
        await CanManageAsync(context, requirement, resource);
    }

    private async Task CanManageAsync(AuthorizationHandlerContext context, GroupUserOperationRequirement requirement,
        GroupUser resource)
    {
        var group = await _groupRepository.GetByIdAsync(resource.GroupId);
        if (group == null)
        {
            context.Fail();
            return;
        }

        var org = _currentContext.GetOrganization(group.OrganizationId);
        if (org == null)
        {
            context.Fail();
        }

        var canAccess = org.Type == OrganizationUserType.Owner ||
                        org.Type == OrganizationUserType.Admin ||
                        org.Permissions.ManageGroups ||
                        await _currentContext.ProviderUserForOrgAsync(org.Id);

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
