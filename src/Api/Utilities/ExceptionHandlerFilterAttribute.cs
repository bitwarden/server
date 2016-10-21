using System;
using Bit.Api.Models.Response;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Api.Utilities
{
    public class ExceptionHandlerFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            var errorModel = new ErrorResponseModel { Message = "An error has occured." };

            var exception = context.Exception;
            if(exception == null)
            {
                // Should never happen.
                return;
            }

            var badRequestException = exception as BadRequestException;
            if(badRequestException != null)
            {
                context.HttpContext.Response.StatusCode = 400;

                if(badRequestException.ModelState != null)
                {
                    errorModel = new ErrorResponseModel(badRequestException.ModelState);
                }
                else
                {
                    errorModel.Message = badRequestException.Message;
                }
            }
            else if(exception is ApplicationException)
            {
                context.HttpContext.Response.StatusCode = 402;
            }
            else if(exception is NotFoundException)
            {
                errorModel.Message = "Resource not found.";
                context.HttpContext.Response.StatusCode = 404;
            }
            else if(exception is SecurityTokenValidationException)
            {
                errorModel.Message = "Invalid token.";
                context.HttpContext.Response.StatusCode = 403;
            }
            else
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ExceptionHandlerFilterAttribute>>();
                logger.LogError(0, exception, exception.Message);

                errorModel.Message = "An unhandled server error has occured.";
                context.HttpContext.Response.StatusCode = 500;
            }

            var env = context.HttpContext.RequestServices.GetRequiredService<IHostingEnvironment>();
            if(env.IsDevelopment())
            {
                errorModel.ExceptionMessage = exception.Message;
                errorModel.ExceptionStackTrace = exception.StackTrace;
                errorModel.InnerExceptionMessage = exception?.InnerException?.Message;
            }

            context.Result = new ObjectResult(errorModel);
        }
    }
}
