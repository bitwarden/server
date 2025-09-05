using Bit.Core.Billing.Commands;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

public abstract class BaseBillingController : Controller
{
    /// <summary>
    /// Processes the result of a billing command and converts it to an appropriate HTTP result response.
    /// </summary>
    /// <remarks>
    /// Result to response mappings:
    /// <list type="bullet">
    /// <item><description><typeparamref name="T"/>: 200 OK</description></item>
    /// <item><description><see cref="Core.Billing.Commands.BadRequest"/>: 400 BAD_REQUEST</description></item>
    /// <item><description><see cref="Core.Billing.Commands.Conflict"/>: 409 CONFLICT</description></item>
    /// <item><description><see cref="Unhandled"/>: 500 INTERNAL_SERVER_ERROR</description></item>
    /// </list>
    /// </remarks>
    /// <typeparam name="T">The type of the successful result.</typeparam>
    /// <param name="result">The result of executing the billing command.</param>
    /// <returns>An HTTP result response representing the outcome of the command execution.</returns>
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
