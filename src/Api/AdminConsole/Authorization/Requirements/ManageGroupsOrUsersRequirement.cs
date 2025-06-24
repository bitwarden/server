using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

public class ManageGroupsOrUsersRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(CurrentContextOrganization organizationClaims, Func<Task<bool>> isProviderUserForOrg) =>
        organizationClaims switch
        {
            { Permissions.ManageGroups: true } or { Permissions.ManageUsers: true } => true,
            _ => await isProviderUserForOrg()
        };
}
