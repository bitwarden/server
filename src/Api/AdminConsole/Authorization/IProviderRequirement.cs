using Bit.Core.AdminConsole.Context;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// A requirement that implements this interface will be handled by <see cref="ProviderRequirementHandler"/>,
/// which calls AuthorizeAsync with the provider details from the route.
/// This is used for simple role-based checks.
/// This may only be used on endpoints with {providerId} in their path.
/// </summary>
public interface IProviderRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Whether to authorize a request that has this requirement.
    /// </summary>
    /// <param name="providerClaims">
    /// The CurrentContextProvider for the user if they are a member of the provider.
    /// This is null if they are not a member.
    /// </param>
    /// <returns>True if the requirement has been satisfied, otherwise false.</returns>
    public bool Authorize(CurrentContextProvider? providerClaims);
}
