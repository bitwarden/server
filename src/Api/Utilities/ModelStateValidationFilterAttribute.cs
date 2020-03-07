using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using InternalApi = Bit.Core.Models.Api;
using PublicApi = Bit.Core.Models.Api.Public;
using System.Linq;

namespace Bit.Api.Utilities
{
    public class ModelStateValidationFilterAttribute : ActionFilterAttribute
    {
        private readonly bool _publicApi;

        public ModelStateValidationFilterAttribute(bool publicApi)
        {
            _publicApi = publicApi;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var model = context.ActionArguments.FirstOrDefault(a => a.Key == "model");
            if(model.Key == "model" && model.Value == null)
            {
                context.ModelState.AddModelError(string.Empty, "Body is empty.");
            }

            if(!context.ModelState.IsValid)
            {
                if(_publicApi)
                {
                    context.Result = new BadRequestObjectResult(new PublicApi.ErrorResponseModel(context.ModelState));
                }
                else
                {
                    context.Result = new BadRequestObjectResult(new InternalApi.ErrorResponseModel(context.ModelState));
                }
            }
        }
    }
}
