#nullable enable

using System.Security.Claims;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Services;

/// <summary>
/// Used to implement <a href="https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased">
/// resource-based authorization</a> checks. This is a wrapper around the
/// <a href="https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authorization.iauthorizationservice?">
/// ASP.Net Core IAuthorizationService</a> in order to provide some of our own helpers and allow easier testing.
/// </summary>
public interface IBitAuthorizationService
{
    /// <summary>
    /// Checks if a user meets the specified requirement for the specified resource.
    /// <exception cref="NotFoundException">If the user does not meet the requirement.</exception>
    /// </summary>
    Task AuthorizeOrThrowAsync(ClaimsPrincipal user, object? resource, IAuthorizationRequirement requirement);

    /// <summary>
    /// Checks if a user meets the specified requirements for the specified resource.
    /// <exception cref="NotFoundException">If the user does not meet the requirements.</exception>
    /// </summary>
    Task AuthorizeOrThrowAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements);

    /// <summary>
    /// Checks if a user meets the specified requirement for the specified resource.
    /// <returns>AuthorizationResult indicating whether the requirement is met or not</returns>
    /// </summary>
    Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IAuthorizationRequirement requirement);

    /// <summary>
    /// Checks if a user meets the specified requirements for the specified resource.
    /// <returns>AuthorizationResult indicating whether the requirements are met or not</returns>
    /// </summary>
    Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements);
}
