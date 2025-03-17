using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.
    OrganizationUserAccountRecoveryDetails;

public class OrganizationUserAccountRecoveryDetailsAuthorizationHandler(ICurrentContext currentContext)
    : AuthorizationHandler<OrganizationUsersAccountRecoveryDetailsOperationRequirement, OrganizationScope>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationUsersAccountRecoveryDetailsOperationRequirement requirement, OrganizationScope resource)
    {
        var authorized = requirement switch
        {
            not null when requirement.Name is nameof(OrganizationUsersAccountRecoveryDetailsOperations.Read) or
                    nameof(OrganizationUsersAccountRecoveryDetailsOperations.ReadAll) =>
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
