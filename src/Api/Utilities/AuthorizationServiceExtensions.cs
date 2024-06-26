using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Utilities;

public static class AuthorizationServiceExtensions
{
    /// <summary>
    /// Checks if a user meets a specific requirement.
    /// </summary>
    /// <param name="service">The <see cref="IAuthorizationService"/> providing authorization.</param>
    /// <param name="user">The user to evaluate the policy against.</param>
    /// <param name="requirement">The requirement to evaluate the policy against.</param>
    /// <returns>
    /// A flag indicating whether requirement evaluation has succeeded or failed.
    /// This value is <value>true</value> when the user fulfills the policy, otherwise <value>false</value>.
    /// </returns>
    public static Task<AuthorizationResult> AuthorizeAsync(this IAuthorizationService service, ClaimsPrincipal user, IAuthorizationRequirement requirement)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        if (requirement == null)
        {
            throw new ArgumentNullException(nameof(requirement));
        }

        return service.AuthorizeAsync(user, resource: null, new[] { requirement });
    }
}
