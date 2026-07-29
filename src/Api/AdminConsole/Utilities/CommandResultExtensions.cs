using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using CommandError = Bit.Core.AdminConsole.Utilities.v2.Error;

namespace Bit.Api.AdminConsole.Utilities;

/// <summary>
/// Maps a <see cref="CommandResult"/>/<see cref="CommandResult{T}"/> to an HTTP response. Shared by
/// <see cref="Bit.Api.AdminConsole.Controllers.BaseAdminConsoleController"/> and Minimal API endpoint handlers.
/// </summary>
public static class CommandResultExtensions
{
    /// <summary>
    /// Maps a void <see cref="CommandResult"/> to an HTTP response.
    /// Returns 204 No Content on success, or the appropriate error status code on failure.
    /// </summary>
    public static IResult ToHttpResult(this CommandResult commandResult) =>
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
    public static IResult ToHttpResult<T>(this CommandResult<T> commandResult, Func<T, IResult> success) =>
        commandResult.Match<IResult>(
            error => MapError(error),
            success
        );

    private static IResult MapError(CommandError error) =>
        error switch
        {
            IValidationError validationError => TypedResults.BitwardenValidationProblem(validationError),
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
