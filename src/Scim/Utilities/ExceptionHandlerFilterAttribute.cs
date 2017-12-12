using Bit.Core.Exceptions;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Scim.Utilities
{
    public class ExceptionHandlerFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            var exception = context.Exception;
            if(exception == null)
            {
                // Should never happen.
                return;
            }

            var error = new ScimError();
            if(exception is BadRequestException)
            {
                context.HttpContext.Response.StatusCode = error.Status = 400;
                error.Detail = exception.Message;
            }
            else if(exception is NotFoundException)
            {
                context.HttpContext.Response.StatusCode = error.Status = 404;
                error.Detail = "Resource not found.";
            }
            else
            {
                context.HttpContext.Response.StatusCode = error.Status = 500;
                error.Detail = "An unhandled server error has occurred.";
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ExceptionHandlerFilterAttribute>>();
                logger.LogError(0, exception, exception.Message);
            }

            context.Result = new ObjectResult(error);
        }
    }
}
