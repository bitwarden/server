using System.Security.Claims;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Utilities;

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
    public static Task<AuthorizationResult> AuthorizeAsync(
        this IAuthorizationService service,
        ClaimsPrincipal user,
        IAuthorizationRequirement requirement
    )
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

    /// <summary>
    /// Performs an authorization check and throws a <see cref="Bit.Core.Exceptions.NotFoundException"/> if the
    /// check fails or the resource is null.
    /// </summary>
    public static async Task AuthorizeOrThrowAsync(
        this IAuthorizationService service,
        ClaimsPrincipal user,
        object resource,
        IAuthorizationRequirement requirement
    )
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(requirement);

        if (resource == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult = await service.AuthorizeAsync(user, resource, requirement);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }
    }
}
