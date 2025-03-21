#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.AdminConsole.OrganizationFeatures;

public class ManageUsersRequirement : IOrganizationRequirement;

public class AdminConsoleRequirementsHandler(ICurrentContext currentContext, IHttpContextAccessor httpContextAccessor)
    : OrganizationRequirementHandler(currentContext, httpContextAccessor)
{
    protected override async Task<bool> HandleOrganizationRequirementAsync(IOrganizationRequirement requirement,
        Guid organizationId, CurrentContextOrganization? organization)
    {
        var authorized = requirement switch
        {
            ManageUsersRequirement => await ManageUsersAsync(organizationId, organization),
            _ => false
        };

        return authorized;
    }

    private async Task<bool> ManageUsersAsync(Guid organizationId, CurrentContextOrganization? organization)
        => organization is
    { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
    { Permissions.ManageUsers: true }
           || await IsProviderForOrganizationAsync(organizationId);
}
