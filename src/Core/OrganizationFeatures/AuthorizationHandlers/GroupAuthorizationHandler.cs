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
        Group resource)
    {
        // Currently all GroupOperationRequirements have the same permission requirements
        if (requirement == GroupOperations.Create ||
            requirement == GroupOperations.Read ||
            requirement == GroupOperations.Update ||
            requirement == GroupOperations.Delete ||
            requirement == GroupOperations.AddUser ||
            requirement == GroupOperations.DeleteUser)
        {
            CanManageGroups(context, requirement, resource);
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
