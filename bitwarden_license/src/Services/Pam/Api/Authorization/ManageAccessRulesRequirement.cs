using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Services.Pam.Api.Authorization;

/// <summary>
/// Requires authority over the organization's access rules: an Owner, an Admin, or a Custom user holding
/// <see cref="Permissions.ManageAccessRules"/>. Providers are not authorized, since access rules gate who can lease
/// credentials.
/// </summary>
public class ManageAccessRulesRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
    {
        var authorized = organizationClaims is
        { Type: OrganizationUserType.Owner }
            or { Type: OrganizationUserType.Admin }
            or { Type: OrganizationUserType.Custom, Permissions.ManageAccessRules: true };

        return Task.FromResult(authorized);
    }
}
