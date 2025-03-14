using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUsersResetPasswordDetails;

public class OrganizationUserResetPasswordDetailsAuthorizationHandler(ICurrentContext currentContext)
    : AuthorizationHandler<OrganizationUsersResetPasswordDetailsOperationRequirement, OrganizationScope>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUsersResetPasswordDetailsOperationRequirement requirement, OrganizationScope resource)
    {
        var authorized = requirement switch
        {
            not null when requirement.Name == nameof(OrganizationUsersResetPasswordDetailsOperations.Read) =>
                await currentContext.ManageResetPassword(resource),
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
