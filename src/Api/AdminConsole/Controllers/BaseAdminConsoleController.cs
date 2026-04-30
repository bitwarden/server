using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

public abstract class BaseAdminConsoleController : Controller
{
    /// <summary>
    /// Maps a void <see cref="CommandResult"/> to an HTTP response.
    /// Returns 204 No Content on success, or the appropriate error status code on failure.
    /// </summary>
    protected static IResult Handle(CommandResult commandResult) =>
        commandResult.Match<IResult>(
            error => MapError(error),
            _ => TypedResults.NoContent()
        );

    /// <summary>
    /// Maps a <see cref="CommandResult{T}"/> to an HTTP response.
    /// On success, delegates to <paramref name="success"/> so the caller can choose the response shape
    /// (e.g. <c>TypedResults.Created</c> for POST, <c>TypedResults.Ok</c> for GET/PUT).
    /// On failure, returns the appropriate error status code.
    /// </summary>
    protected static IResult Handle<T>(CommandResult<T> commandResult, Func<T, IResult> success) =>
        commandResult.Match<IResult>(
            error => MapError(error),
            success
        );

    private static IResult MapError(Error error) =>
        error switch
        {
            BadRequestError badRequest => TypedResults.BadRequest(new ErrorResponseModel(badRequest.Message)),
            NotFoundError notFound => TypedResults.NotFound(new ErrorResponseModel(notFound.Message)),
            ConflictError conflict => TypedResults.Json(
                new ErrorResponseModel(conflict.Message),
                statusCode: StatusCodes.Status409Conflict),
            InternalError internalError => TypedResults.Json(
                new ErrorResponseModel(internalError.Message),
                statusCode: StatusCodes.Status500InternalServerError),
            _ => TypedResults.Json(
                new ErrorResponseModel(error.Message),
                statusCode: StatusCodes.Status500InternalServerError
            )
        };
}
