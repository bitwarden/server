using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

class GroupAuthHandler : AuthorizationHandler<GroupOperationRequirement, Group>
{
    private readonly ICurrentContext _currentContext;

    public GroupAuthHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupOperationRequirement requirement,
        Group resource)
    {
        // Currently all GroupOperationRequirements have the same permission requirements
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
