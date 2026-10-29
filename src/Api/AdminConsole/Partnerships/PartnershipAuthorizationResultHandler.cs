using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;

namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Adds the partnership error envelope to 401 and 403 responses that authorization middleware returns on a partnership
/// endpoint path, before any partnership filter runs. Every other path keeps the framework's default response.
/// </summary>
public sealed class PartnershipAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);

        if (context.Response.HasStarted ||
            !context.Request.Path.StartsWithSegments(PartnershipRoutes.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var result = context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => PartnershipErrorResults.Unauthorized(context),
            StatusCodes.Status403Forbidden => PartnershipErrorResults.Forbidden(context),
            _ => null,
        };
        if (result is not null)
        {
            await result.ExecuteResultAsync(new ActionContext(context, context.GetRouteData(), new ActionDescriptor()));
        }
    }
}
