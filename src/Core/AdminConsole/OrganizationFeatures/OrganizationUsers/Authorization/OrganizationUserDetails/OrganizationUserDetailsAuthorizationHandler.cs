#nullable enable
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUserDetails;

public class OrganizationUserDetailsAuthorizationHandler(ICurrentContext currentContext) :
    AuthorizationHandler<OrganizationUserDetailsOperationRequirement, OrganizationScope>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUserDetailsOperationRequirement requirement,
        OrganizationScope organizationScope)
    {
        var authorized = requirement switch
        {
            not null when requirement.Name == nameof(OrganizationUserDetailsOperations.Read) => await currentContext
                .ManageUsers(organizationScope),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement!);
            return;
        }

        context.Fail();
    }
}
