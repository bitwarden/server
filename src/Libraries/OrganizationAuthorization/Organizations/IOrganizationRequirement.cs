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
    /// <param name="isOrganizationManagedByProvider">
    /// A callback that returns true if the organization is managed by a provider, otherwise false.
    /// Scoped to the organizations the user is a member of: this returns false for a user with no
    /// membership in the organization (a ProviderUser, for example) even if a provider manages it.
    /// This requires a database query, call it last.
    /// </param>
    /// <returns>True if the requirement has been satisfied, otherwise false.</returns>
    public Task<bool> AuthorizeAsync(
        CurrentContextOrganization? organizationClaims,
        Func<Task<bool>> isProviderUserForOrg,
        Func<Task<bool>> isOrganizationManagedByProvider);
}
