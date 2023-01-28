using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using InternalApi = Bit.Core.Models.Api;

namespace Bit.SharedWeb.Utilities;

public class ExceptionHandlerFilterAttribute : ExceptionFilterAttribute
{
    public ExceptionHandlerFilterAttribute()
    {
    }

    public override void OnException(ExceptionContext context)
    {
        var errorMessage = "An error has occurred.";

        var exception = context.Exception;
        if (exception == null)
        {
            // Should never happen.
            return;
        }

        InternalApi.ErrorResponseModel internalErrorModel = null;
        if (exception is BadRequestException badRequestException)
        {
            context.HttpContext.Response.StatusCode = 400;
            if (badRequestException.ModelState != null)
            {
                internalErrorModel = new InternalApi.ErrorResponseModel(badRequestException.ModelState);
            }
            else
            {
                errorMessage = badRequestException.Message;
            }
        }
        else if (exception is GatewayException)
        {
            errorMessage = exception.Message;
            context.HttpContext.Response.StatusCode = 400;
        }
        else if (exception is NotSupportedException && !string.IsNullOrWhiteSpace(exception.Message))
        {
            errorMessage = exception.Message;
            context.HttpContext.Response.StatusCode = 400;
        }
        else if (exception is ApplicationException)
        {
            context.HttpContext.Response.StatusCode = 402;
        }
        else if (exception is NotFoundException)
        {
            errorMessage = "Resource not found.";
            context.HttpContext.Response.StatusCode = 404;
        }
        else if (exception is SecurityTokenValidationException)
        {
            errorMessage = "Invalid token.";
            context.HttpContext.Response.StatusCode = 403;
        }
        else if (exception is UnauthorizedAccessException)
        {
            errorMessage = "Unauthorized.";
            context.HttpContext.Response.StatusCode = 401;
        }
        else
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ExceptionHandlerFilterAttribute>>();
            logger.LogError(0, exception, exception.Message);
            errorMessage = "An unhandled server error has occurred.";
            context.HttpContext.Response.StatusCode = 500;
        }

        var errorModel = internalErrorModel ?? new InternalApi.ErrorResponseModel(errorMessage);
        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (env.IsDevelopment())
        {
            errorModel.ExceptionMessage = exception.Message;
            errorModel.ExceptionStackTrace = exception.StackTrace;
            errorModel.InnerExceptionMessage = exception?.InnerException?.Message;
        }
        context.Result = new ObjectResult(errorModel);
    }
}
