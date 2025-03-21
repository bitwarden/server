using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUserGroups;

public class OrganizationUserGroupsAuthorizationHandler(ICurrentContext currentContext)
    : AuthorizationHandler<OrganizationUserGroupOperationRequirement, OrganizationScope>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUserGroupOperationRequirement requirement,
        OrganizationScope resource)
    {
        var authorized = requirement switch
        {
            not null when requirement.Name == nameof(OrganizationUserGroupOperations.ReadAllIds) =>
                await CanReadGroupIdsAsync(resource),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement!);
            return;
        }

        context.Fail();
    }

    private async Task<bool> CanReadGroupIdsAsync(OrganizationScope organizationId) =>
        await currentContext.ManageUsers(organizationId) || await currentContext.ManageGroups(organizationId);
}
