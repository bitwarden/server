using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.SharedWeb.Utilities;

public class ModelStateValidationFilterAttribute : ActionFilterAttribute
{
    public ModelStateValidationFilterAttribute() { }

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
}
