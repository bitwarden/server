using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Bit.Admin.Auth.Filters;

/// <summary>
/// Short-circuits with 404 when AdminSettings.EnablePasswordlessLogin is false. Apply to
/// email magic-link actions so that operators who have disabled passwordless login (to
/// force SSO-only access) see no reachable surface for the disabled flow.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePasswordlessLoginAttribute : Attribute, IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var settings = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<AdminSettings>>().Value;
        if (!settings.EnablePasswordlessLogin)
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
