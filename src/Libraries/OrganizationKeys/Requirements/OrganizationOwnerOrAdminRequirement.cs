using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;

namespace Bit.OrganizationKeys.Requirements;

/// <summary>
/// Requires that the user is an Owner or an Admin of the organization. Custom
/// users are not authorized. Provider users who are not members of the
/// organization are not authorized.
/// </summary>
public class OrganizationOwnerOrAdminRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
        => Task.FromResult(organizationClaims is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin });
}
