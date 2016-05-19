using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Bit.Api.Models.Response;
using System.Linq;

namespace Bit.Api.Utilities
{
    public class ModelStateValidationFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var model = context.ActionArguments.FirstOrDefault(a => a.Key == "model");
            if(model.Key == "model" && model.Value == null)
            {
                context.ModelState.AddModelError(string.Empty, "Body is empty.");
            }

            if(!context.ModelState.IsValid)
            {
                context.Result = new BadRequestObjectResult(new ErrorResponseModel(context.ModelState));
            }
        }
    }
}
