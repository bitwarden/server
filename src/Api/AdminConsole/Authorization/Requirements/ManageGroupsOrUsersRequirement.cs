using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

public class ManageGroupsOrUsersRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(CurrentContextOrganization? organizationClaims, Func<Task<bool>> isProviderUserForOrg) =>
        organizationClaims switch
        {
            { Type: OrganizationUserType.Owner } => true,
            { Type: OrganizationUserType.Admin } => true,
            { Permissions.ManageGroups: true } => true,
            { Permissions.ManageUsers: true } => true,
            _ => await isProviderUserForOrg()
        };
}
