#nullable enable

using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

public class ManageAccountRecoveryRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
        => organizationClaims switch
        {
            { Type: OrganizationUserType.Owner } => true,
            { Type: OrganizationUserType.Admin } => true,
            { Permissions.ManageResetPassword: true } => true,
            _ => await isProviderUserForOrg()
        };
}
