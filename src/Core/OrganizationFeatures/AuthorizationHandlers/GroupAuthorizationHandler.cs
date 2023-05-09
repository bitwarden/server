#nullable enable

using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

class GroupAuthorizationHandler : AuthorizationHandler<GroupOperationRequirement, Group>
{
    private readonly ICurrentContext _currentContext;

    public GroupAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement,
        Group? resource)
    {
        if (resource == null)
        {
            context.Fail();
            return;
        }

        switch (requirement)
        {
            case not null when requirement == GroupOperations.Create:
                await CanCreateAsync(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Read:
                await CanReadAsync(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Update:
                await CanUpdateAsync(context, requirement, resource);
                break;

            case not null when requirement == GroupOperations.Delete:
                await CanDeleteAsync(context, requirement, resource);
                break;
        }

        await Task.CompletedTask;
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        await CanManageAsync(context, requirement, resource);
    }

    private async Task CanReadAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        await CanManageAsync(context, requirement, resource);
    }

    private async Task CanUpdateAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        await CanManageAsync(context, requirement, resource);
    }

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        await CanManageAsync(context, requirement, resource);
    }

    private async Task CanManageAsync(AuthorizationHandlerContext context, GroupOperationRequirement requirement, Group resource)
    {
        var org = _currentContext.GetOrganization(resource.OrganizationId);
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
