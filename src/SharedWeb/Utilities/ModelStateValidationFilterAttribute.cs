using Bit.Core;
using Bit.Core.Models.Api;
using Bit.HttpExtensions;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SharedWeb.Utilities;

public class ModelStateValidationFilterAttribute : ActionFilterAttribute
{
    public ModelStateValidationFilterAttribute()
    {
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var model = context.ActionArguments.FirstOrDefault(a => a.Key == "model");
        if (model.Key == "model" && model.Value == null)
        {
            context.ModelState.AddModelError(string.Empty, "Body is empty.");
        }

        if (!context.ModelState.IsValid)
        {
            OnModelStateInvalid(context);
        }
    }

    protected virtual void OnModelStateInvalid(ActionExecutingContext context)
    {
        context.Result = new BadRequestObjectResult(new ErrorResponseModel(context.ModelState));
    }

    /// <summary>
    /// The coded problem document for this failure, or null when the surface has not been switched over to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Offered rather than applied, because the shape is a breaking change for whoever is reading it: a caller
    /// parsing <c>validationErrors</c> finds nothing it recognises in an RFC 7807 body. Each surface opts in on
    /// its own schedule, and none is switched by inheriting from this.
    /// </para>
    /// </remarks>
    protected static IActionResult? TryCodedProblem(ActionExecutingContext context)
    {
        var featureService = context.HttpContext.RequestServices.GetService<IFeatureService>();
        if (featureService?.IsEnabled(FeatureFlagKeys.CodedValidationProblems) != true)
        {
            return null;
        }

        return new ObjectResult(ValidationProblemFactory.FromModelState(context.ModelState))
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    }
}
