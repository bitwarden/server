using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using CommandError = Bit.Core.AdminConsole.Utilities.v2.Error;

namespace Bit.Api.AdminConsole.Utilities;

/// <summary>
/// Maps a <see cref="CommandResult"/>/<see cref="CommandResult{T}"/> to an HTTP response.
/// Shared by MVC controllers (via <c>BaseAdminConsoleController.Handle</c>) and Minimal API endpoint handlers.
/// </summary>
public static class CommandResultExtensions
{
    /// <summary>
    /// Returns 204 No Content on success, or the mapped error status code on failure.
    /// </summary>
    public static IResult ToHttpResult(this CommandResult commandResult) =>
        commandResult.Match<IResult>(
            error => MapError(error),
            _ => TypedResults.NoContent()
        );

    /// <summary>
    /// Delegates to <paramref name="success"/> on success so the caller chooses the response shape, or returns
    /// the mapped error status code on failure.
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
