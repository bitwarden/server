using System;
using InternalApi = Bit.Core.Models.Api;
using PublicApi = Bit.Core.Models.Api.Public;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Stripe;

namespace Bit.Api.Utilities
{
    public class ExceptionHandlerFilterAttribute : ExceptionFilterAttribute
    {
        private readonly bool _publicApi;

        public ExceptionHandlerFilterAttribute(bool publicApi)
        {
            _publicApi = publicApi;
        }

        public override void OnException(ExceptionContext context)
        {
            var errorMessage = "An error has occurred.";

            var exception = context.Exception;
            if(exception == null)
            {
                // Should never happen.
                return;
            }

            PublicApi.ErrorResponseModel publicErrorModel = null;
            InternalApi.ErrorResponseModel internalErrorModel = null;
            if(exception is BadRequestException badRequestException)
            {
                context.HttpContext.Response.StatusCode = 400;
                if(badRequestException.ModelState != null)
                {
                    if(_publicApi)
                    {
                        publicErrorModel = new PublicApi.ErrorResponseModel(badRequestException.ModelState);
                    }
                    else
                    {
                        internalErrorModel = new InternalApi.ErrorResponseModel(badRequestException.ModelState);
                    }
                }
                else
                {
                    errorMessage = badRequestException.Message;
                }
            }
            else if(exception is StripeException stripeException &&
                stripeException?.StripeError?.ErrorType == "card_error")
            {
                context.HttpContext.Response.StatusCode = 400;
                if(_publicApi)
                {
                    publicErrorModel = new PublicApi.ErrorResponseModel(stripeException.StripeError.Parameter,
                        stripeException.Message);
                }
                else
                {
                    internalErrorModel = new InternalApi.ErrorResponseModel(stripeException.StripeError.Parameter,
                        stripeException.Message);
                }
            }
            else if(exception is GatewayException)
            {
                errorMessage = exception.Message;
                context.HttpContext.Response.StatusCode = 400;
            }
            else if(exception is NotSupportedException && !string.IsNullOrWhiteSpace(exception.Message))
            {
                errorMessage = exception.Message;
                context.HttpContext.Response.StatusCode = 400;
            }
            else if(exception is ApplicationException)
            {
                context.HttpContext.Response.StatusCode = 402;
            }
            else if(exception is NotFoundException)
            {
                errorMessage = "Resource not found.";
                context.HttpContext.Response.StatusCode = 404;
            }
            else if(exception is SecurityTokenValidationException)
            {
                errorMessage = "Invalid token.";
                context.HttpContext.Response.StatusCode = 403;
            }
            else if(exception is UnauthorizedAccessException)
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

            if(_publicApi)
            {
                var errorModel = publicErrorModel ?? new PublicApi.ErrorResponseModel(errorMessage);
                context.Result = new ObjectResult(errorModel);
            }
            else
            {
                var errorModel = internalErrorModel ?? new InternalApi.ErrorResponseModel(errorMessage);
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
}
