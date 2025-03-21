#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public class ManageUsersRequirement : IOrganizationRequirement;

public class AdminConsoleRequirementsHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<IOrganizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        IOrganizationRequirement requirement)
    {
        var organizationId = httpContextAccessor.GetOrganizationId();
        if (organizationId is null)
        {
            return;
        }

        var organization = currentContext.GetOrganization(organizationId.Value);

        var authorized = requirement switch
        {
            ManageUsersRequirement => await ManageUsersAsync(organizationId.Value, organization),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> ManageUsersAsync(Guid organizationId, CurrentContextOrganization? organization)
        => organization is
    { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
    { Permissions.ManageUsers: true }
           || await currentContext.ProviderUserForOrgAsync(organizationId);
}
