#nullable enable
using Bit.Core.Billing.Commands;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

public abstract class BaseBillingController : Controller
{
    protected static IResult Handle<T>(BillingCommandResult<T> result) =>
        result.Match<IResult>(
            TypedResults.Ok,
            badRequest => Error.BadRequest(badRequest.Response),
            conflict => Error.Conflict(conflict.Response),
            unhandled => Error.ServerError(unhandled.Response, unhandled.Exception));

    protected static class Error
    {
        public static BadRequest<ErrorResponseModel> BadRequest(string message) =>
            TypedResults.BadRequest(new ErrorResponseModel(message));

        public static JsonHttpResult<ErrorResponseModel> Conflict(string message) =>
            TypedResults.Json(
                new ErrorResponseModel(message),
                statusCode: StatusCodes.Status409Conflict);

        public static NotFound<ErrorResponseModel> NotFound() =>
            TypedResults.NotFound(new ErrorResponseModel("Resource not found."));

        public static JsonHttpResult<ErrorResponseModel> ServerError(
            string message = "Something went wrong with your request. Please contact support for assistance.",
            Exception? exception = null) =>
            TypedResults.Json(
                exception == null ? new ErrorResponseModel(message) : new ErrorResponseModel(message)
                {
                    ExceptionMessage = exception.Message,
                    ExceptionStackTrace = exception.StackTrace
                },
                statusCode: StatusCodes.Status500InternalServerError);

        public static JsonHttpResult<ErrorResponseModel> Unauthorized(string message = "Unauthorized.") =>
            TypedResults.Json(
                new ErrorResponseModel(message),
                statusCode: StatusCodes.Status401Unauthorized);
    }
}
