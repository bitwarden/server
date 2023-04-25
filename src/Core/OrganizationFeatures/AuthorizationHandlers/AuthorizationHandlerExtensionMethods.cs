using System.Security.Claims;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public static class AuthorizationHandlerExtensionMethods
{
    /// <summary>
    /// A wrapper around AuthorizeAsync which will throw if authorization fails. 
    /// </summary>
    public static async Task AuthorizeOrThrowAsync(this IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        object? resource,
        IAuthorizationRequirement requirement)
    {
        AuthorizeOrThrowAsync(async () => await authorizationService.AuthorizeAsync(user, resource, requirement));
    }

    /// <summary>
    /// A wrapper around AuthorizeAsync which will throw if authorization fails. 
    /// </summary>
    public static async Task AuthorizeOrThrowAsync(this IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        AuthorizeOrThrowAsync(async () => await authorizationService.AuthorizeAsync(user, resource, requirements));
    }

    private static async Task AuthorizeOrThrowAsync(Func<Task<AuthorizationResult>> getAuthorizationResult)
    {
        var authorizationResult = await getAuthorizationResult();
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }
    }
}
