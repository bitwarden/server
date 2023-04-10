using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

class GroupsAuthHandler : AuthorizationHandler<GroupOperationRequirement, Group>
{
    private readonly ICurrentContext _currentContext;

    public GroupsAuthHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement,
        Group resource)
    {
        if (requirement == GroupsOperations.ReadGroupRequirement)
        {
            await ReadGroupAsync(context, requirement, resource);
            return;
        }
    }

    private async Task ReadGroupAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement,
        Group resource)
    {
        var org = _currentContext.GetOrganization(resource.OrganizationId);
        var canAccess = org.Type == OrganizationUserType.Owner ||
                        org.Type == OrganizationUserType.Admin ||
                        (org.Permissions?.ManageGroups ?? false);

        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
