using Bit.Api.Models.Public.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using InternalApi = Bit.Core.Models.Api;

namespace Bit.Api.Utilities;

public class ModelStateValidationFilterAttribute : SharedWeb.Utilities.ModelStateValidationFilterAttribute
{
    private readonly bool _publicApi;

    public ModelStateValidationFilterAttribute(bool publicApi)
    {
        _publicApi = publicApi;
    }

    protected override void OnModelStateInvalid(ActionExecutingContext context)
    {
        if (_publicApi)
        {
            context.Result = new BadRequestObjectResult(new ErrorResponseModel(context.ModelState));
        }
        else
        {
            context.Result = new BadRequestObjectResult(new InternalApi.ErrorResponseModel(context.ModelState));
        }
    }
}
