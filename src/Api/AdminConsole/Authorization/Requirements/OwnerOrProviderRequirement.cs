using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

/// <summary>
/// Requires that the user is an Owner of the organization or a provider for the organization.
/// </summary>
public class OwnerOrProviderRequirement : IOrganizationRequirement
{
    public async Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
        => organizationClaims switch
        {
            { Type: OrganizationUserType.Owner } => true,
            _ => await isProviderUserForOrg()
        };
}
