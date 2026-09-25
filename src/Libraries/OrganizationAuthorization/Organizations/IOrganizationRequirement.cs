#nullable enable

using Bit.Core.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// A requirement that implements this interface will be handled by <see cref="OrganizationRequirementHandler"/>,
/// which calls AuthorizeAsync with the organization details from the route.
/// This is used for simple role-based checks.
/// This may only be used on endpoints with {orgId} in their path.
/// </summary>
public interface IOrganizationRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Whether to authorize a request that has this requirement.
    /// </summary>
    /// <param name="organizationClaims">
    /// The CurrentContextOrganization for the user if they are a member of the organization.
    /// This is null if they are not a member.
    /// </param>
    /// <param name="isProviderUserForOrg">
    /// A callback that returns true if the user is a ProviderUser that manages the organization, otherwise false.
    /// This requires a database query, call it last.
    /// </param>
    /// <returns>True if the requirement has been satisfied, otherwise false.</returns>
    public Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg);
}
