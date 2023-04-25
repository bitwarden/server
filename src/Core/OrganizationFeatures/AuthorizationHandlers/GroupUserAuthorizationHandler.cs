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
        if (resource == null)
        {
            context.Fail();
            return;
        }

        switch (requirement)
        {
            case not null when requirement == GroupUserOperations.Create:
            case not null when requirement == GroupUserOperations.Delete:
                CreateOrDeleteCheckAsync(context, requirement, resource);
                break;

        }

        await Task.CompletedTask;
    }

    private async Task CreateOrDeleteCheckAsync(AuthorizationHandlerContext context, GroupUserOperationRequirement requirement,
        GroupUser resource)
    {
        var group = await _groupRepository.GetByIdAsync(resource.GroupId);
        if (group == null)
        {
            context.Fail();
            return;
        }

        // TODO: providers need to be included in the claims
        var org = _currentContext.GetOrganization(group.OrganizationId);
        var canAccess = org.Type == OrganizationUserType.Owner ||
                        org.Type == OrganizationUserType.Admin ||
                        org.Permissions.ManageGroups;

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
