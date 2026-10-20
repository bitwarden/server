using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Returns invalid models and unhandled exceptions on partnership controllers in the partnership error envelope.
/// Applied in place of the default API error filters by <see cref="Utilities.PublicApiControllersModelConvention"/>.
/// </summary>
public sealed class PartnershipErrorFilter : IActionFilter, IExceptionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var model = context.ActionArguments.FirstOrDefault(a => a.Key == "model");
        if (!context.ModelState.IsValid || (model.Key == "model" && model.Value == null))
        {
            context.Result = PartnershipErrorResults.InvalidRequest(context.HttpContext);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    public void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<PartnershipErrorFilter>>();
        // The exception message can carry request data such as external IDs, so only its type and stack are logged.
        logger.LogError("Unhandled {ExceptionType} on a partnership endpoint. RequestId: {RequestId}{NewLine}{StackTrace}",
            context.Exception.GetType().FullName, PartnershipRequestId.Get(context.HttpContext), Environment.NewLine,
            context.Exception.StackTrace);

        context.Result = PartnershipErrorResults.InternalError(context.HttpContext);
        context.ExceptionHandled = true;
    }
}
