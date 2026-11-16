using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Bit.Admin.Auth.Filters;

/// <summary>
/// Short-circuits with 404 when AdminSettings.OidcEnabled is false. Apply to actions that
/// should only be reachable when the operator has configured an upstream OIDC provider;
/// this keeps feature-gated endpoints invisible rather than exposing an obvious "disabled"
/// surface.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireOidcAttribute : Attribute, IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var settings = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<AdminSettings>>().Value;
        if (!settings.OidcEnabled)
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
