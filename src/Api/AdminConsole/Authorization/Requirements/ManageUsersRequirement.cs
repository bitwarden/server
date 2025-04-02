#nullable enable

using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

public class ManageUsersRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
        => organizationClaims is
    { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
    { Permissions.ManageUsers: true }
            || await isProviderUserForOrg();
}
