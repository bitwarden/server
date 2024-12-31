﻿using System.Text;
using Bit.Api.Models.Public.Response;
using Bit.Core.Billing;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using InternalApi = Bit.Core.Models.Api;

namespace Bit.Api.Utilities;

public class ExceptionHandlerFilterAttribute : ExceptionFilterAttribute
{
    private readonly bool _publicApi;

    public ExceptionHandlerFilterAttribute(bool publicApi)
    {
        _publicApi = publicApi;
    }

    public override void OnException(ExceptionContext context)
    {
        var errorMessage = GetFormattedMessageFromErrorCode(context);

        var exception = context.Exception;
        if (exception == null)
        {
            // Should never happen.
            return;
        }

        ErrorResponseModel publicErrorModel = null;
        InternalApi.ErrorResponseModel internalErrorModel = null;
        if (exception is BadRequestException badRequestException)
        {
            context.HttpContext.Response.StatusCode = 400;
            if (badRequestException.ModelState != null)
            {
                if (_publicApi)
                {
                    publicErrorModel = new ErrorResponseModel(badRequestException.ModelState);
                }
                else
                {
                    internalErrorModel = new InternalApi.ErrorResponseModel(badRequestException.ModelState);
                }
            }
        }
        else if (exception is StripeException { StripeError.Type: "card_error" } stripeCardErrorException)
        {
            context.HttpContext.Response.StatusCode = 400;
            if (_publicApi)
            {
                publicErrorModel = new ErrorResponseModel(stripeCardErrorException.StripeError.Param,
                    stripeCardErrorException.Message);
            }
            else
            {
                internalErrorModel = new InternalApi.ErrorResponseModel(stripeCardErrorException.StripeError.Param,
                    stripeCardErrorException.Message);
            }
        }
        else if (exception is GatewayException)
        {
            context.HttpContext.Response.StatusCode = 400;
        }
        else if (exception is BillingException billingException)
        {
            errorMessage = billingException.Response;
            context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
        else if (exception is StripeException stripeException)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ExceptionHandlerFilterAttribute>>();

            var error = stripeException.Message;

            if (stripeException.StripeError != null)
            {
                var stringBuilder = new StringBuilder();

                if (!string.IsNullOrEmpty(stripeException.StripeError.Code))
                {
                    stringBuilder.Append($"{stripeException.StripeError.Code} | ");
                }

                stringBuilder.Append(stripeException.StripeError.Message);

                if (!string.IsNullOrEmpty(stripeException.StripeError.DocUrl))
                {
                    stringBuilder.Append($" > {stripeException.StripeError.DocUrl}");
                }

                error = stringBuilder.ToString();
            }

            logger.LogError("An unhandled error occurred while communicating with Stripe: {Error}", error);
            errorMessage = "Something went wrong with your request. Please contact support.";
            context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
        else if (exception is NotSupportedException && !string.IsNullOrWhiteSpace(exception.Message))
        {
            context.HttpContext.Response.StatusCode = 400;
        }
        else if (exception is ApplicationException)
        {
            context.HttpContext.Response.StatusCode = 402;
        }
        else if (exception is NotFoundException)
        {
            errorMessage = GetFormattedMessageFromErrorCode(context, ErrorCode.CommonResourceNotFound);
            context.HttpContext.Response.StatusCode = 404;
        }
        else if (exception is SecurityTokenValidationException)
        {
            errorMessage = GetFormattedMessageFromErrorCode(context, ErrorCode.CommonInvalidToken);
            context.HttpContext.Response.StatusCode = 403;
        }
        else if (exception is UnauthorizedAccessException)
        {
            errorMessage = GetFormattedMessageFromErrorCode(context, ErrorCode.CommonUnauthorized);
            context.HttpContext.Response.StatusCode = 401;
        }
        else if (exception is ConflictException)
        {
            errorMessage = exception.Message;
            context.HttpContext.Response.StatusCode = 409;
        }
        else if (exception is AggregateException aggregateException)
        {
            context.HttpContext.Response.StatusCode = 400;
            var errorValues = aggregateException.InnerExceptions.Select(ex => ex.Message);
            if (_publicApi)
            {
                publicErrorModel = new ErrorResponseModel(errorMessage, errorValues);
            }
            else
            {
                internalErrorModel = new InternalApi.ErrorResponseModel(errorMessage, errorValues);
            }
        }
        else
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ExceptionHandlerFilterAttribute>>();
            logger.LogError(0, exception, exception.Message);
            errorMessage = GetFormattedMessageFromErrorCode(context, ErrorCode.CommonUnhandledError);
            context.HttpContext.Response.StatusCode = 500;
        }

        if (_publicApi)
        {
            var errorModel = publicErrorModel ?? new ErrorResponseModel(errorMessage);
            context.Result = new ObjectResult(errorModel);
        }
        else
        {
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

    private string GetFormattedMessageFromErrorCode(ExceptionContext context, ErrorCode? alternativeErrorCode = null)
    {
        var errorMessageService = context.HttpContext.RequestServices.GetRequiredService<IErrorMessageService>();

        return errorMessageService.GetErrorMessage(context.Exception, alternativeErrorCode);
    }
}
