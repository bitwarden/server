using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

/// <summary>
/// Requires that the user is a member of the organization.
/// </summary>
public class MemberRequirement : IOrganizationRequirement
{
    public Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg)
        => Task.FromResult(organizationClaims is not null);
}
