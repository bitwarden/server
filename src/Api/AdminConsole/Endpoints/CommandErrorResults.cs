using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bit.Api.AdminConsole.Endpoints;

public static class CommandErrorResults
{
    public static IResult ToResult(Error error) =>
        error switch
        {
            IValidationError validationError => TypedResults.BitwardenValidationProblem(validationError),
            _ => ToPublicResult(error),
        };

    public static IResult ToPublicResult(Error error) =>
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
                statusCode: StatusCodes.Status500InternalServerError)
        };

    extension<T>(CommandResult<T> result)
    {
        public IResult ToResult(Func<T, IResult> onSuccess) => result.Match(ToResult, onSuccess);
    }

    extension(CommandResult result)
    {
        public IResult ToResult() => result.Match(ToResult, _ => TypedResults.NoContent());
    }
}
