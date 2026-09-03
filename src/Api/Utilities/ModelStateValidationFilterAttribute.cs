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

    /// <remarks>
    /// Only the internal API takes the coded document. The public API's error shape is a published contract with
    /// its own versioning, so it keeps answering as it always has until that contract is revised deliberately.
    /// </remarks>
    protected override void OnModelStateInvalid(ActionExecutingContext context)
    {
        if (_publicApi)
        {
            context.Result = new BadRequestObjectResult(new ErrorResponseModel(context.ModelState));
            return;
        }

        context.Result = TryCodedProblem(context)
            ?? new BadRequestObjectResult(new InternalApi.ErrorResponseModel(context.ModelState));
    }
}
