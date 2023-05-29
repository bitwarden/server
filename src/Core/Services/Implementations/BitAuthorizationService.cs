#nullable enable

using System.Security.Claims;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Services;

/// <summary>
/// A wrapper around the default AuthorizationService implementation, to provide some helper methods and wrap
/// extension methods so that we can assert calls to them in tests.
/// </summary>
public class BitAuthorizationService : IBitAuthorizationService
{
    private readonly IAuthorizationService _defaultAuthorizationService;

    public BitAuthorizationService(IAuthorizationService defaultAuthorizationService)
    {
        _defaultAuthorizationService = defaultAuthorizationService;
    }

    public async Task AuthorizeOrThrowAsync(ClaimsPrincipal user, object? resource, IAuthorizationRequirement requirement)
        => await AuthorizeOrThrowAsync(user, resource, new[] { requirement });

    public async Task AuthorizeOrThrowAsync(ClaimsPrincipal user, IAuthorizationRequirement requirement)
        => await AuthorizeOrThrowAsync(user, null, new[] { requirement });

    public async Task AuthorizeOrThrowAsync(ClaimsPrincipal user, IEnumerable<IAuthorizationRequirement> requirements)
        => await AuthorizeOrThrowAsync(user, null, requirements);

    public async Task AuthorizeOrThrowAsync(ClaimsPrincipal user, object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        var result = await AuthorizeAsync(user, resource, requirements);
        if (!result.Succeeded)
        {
            // Use NotFoundException to prevent enumeration of resources by an unauthorized user
            throw new NotFoundException();
        }
    }

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
        IAuthorizationRequirement requirement)
        => await _defaultAuthorizationService.AuthorizeAsync(user, resource, requirement);

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
        => await _defaultAuthorizationService.AuthorizeAsync(user, resource, requirements);
}
