using Bit.Core.Exceptions;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Scim.Utilities;

public class ExceptionHandlerFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var exception = context.Exception;
        if (exception == null)
        {
            // Should never happen.
            return;
        }

        int statusCode = StatusCodes.Status500InternalServerError;
        var scimErrorResponseModel = new ScimErrorResponseModel { Detail = exception.Message };

        if (exception is NotFoundException)
        {
            statusCode = StatusCodes.Status404NotFound;
        }
        else if (exception is BadRequestException)
        {
            statusCode = StatusCodes.Status400BadRequest;
        }
        else if (exception is ConflictException)
        {
            statusCode = StatusCodes.Status409Conflict;
        }

        scimErrorResponseModel.Status = statusCode;

        context.HttpContext.Response.StatusCode = statusCode;
        context.Result = new ObjectResult(scimErrorResponseModel);
    }
}
