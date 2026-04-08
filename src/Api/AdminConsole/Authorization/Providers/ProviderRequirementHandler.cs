using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization.Providers;

/// <summary>
/// Handles any requirement that implements <see cref="IProviderRequirement"/>.
/// Retrieves the Provider ID from the route and then passes the provider claims to the requirement's AuthorizeAsync
/// callback to determine whether the action is authorized.
/// </summary>
public class ProviderRequirementHandler(
    IHttpContextAccessor httpContextAccessor,
    IUserService userService)
    : AuthorizationHandler<IProviderRequirement>
{
    public const string NoHttpContextError = "This method should only be called in the context of an HTTP Request.";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, IProviderRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException(NoHttpContextError);
        }

        var providerId = httpContext.GetProviderId();

        var userId = userService.GetProperUserId(httpContext.User);
        if (userId == null)
        {
            return Task.CompletedTask;
        }

        var providerClaims = httpContext.User.GetCurrentContextProvider(providerId);

        if (requirement.Authorize(providerClaims))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
