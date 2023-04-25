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
            // Currently all GroupOperationRequirements have the same permission requirements,
            // but create separate private methods if they start to diverge
            case not null when requirement == GroupOperations.Create:
            case not null when requirement == GroupOperations.Read:
            case not null when requirement == GroupOperations.Update:
            case not null when requirement == GroupOperations.Delete:
            case not null when requirement == GroupOperations.AddUser:
            case not null when requirement == GroupOperations.DeleteUser:
                CanManageGroups(context, requirement, resource);
                break;
        }

        await Task.CompletedTask;
    }

    private void CanManageGroups(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement,
        Group resource)
    {
        // TODO: providers need to be included in the claims
        var org = _currentContext.GetOrganization(resource.OrganizationId);
        var canAccess = org.Type == OrganizationUserType.Owner ||
                        org.Type == OrganizationUserType.Admin ||
                        org.Permissions.ManageGroups;

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
