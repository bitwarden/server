#nullable enable

using System.Security.Claims;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Services;

public class BitAuthorizationService : IBitAuthorizationService
{
    private readonly IAuthorizationService _defaultAuthorizationService;

    public BitAuthorizationService(IAuthorizationService defaultAuthorizationService)
    {
        _defaultAuthorizationService = defaultAuthorizationService;
    }

    public async Task AuthorizeOrThrowAsync(ClaimsPrincipal user, object? resource, IAuthorizationRequirement requirement)
        => await AuthorizeOrThrowAsync(user, resource, new[] { requirement });

    public async Task AuthorizeOrThrowAsync(ClaimsPrincipal user, object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        var result = await AuthorizeAsync(user, resource, requirements);
        if (!result.Succeeded)
        {
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
