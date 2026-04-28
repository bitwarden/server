using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

public abstract class BaseAdminConsoleController : Controller
{
    protected static IResult Handle(CommandResult commandResult) =>
        commandResult.Match<IResult>(
            error => error switch
            {
                BadRequestError badRequest => Error.BadRequest(badRequest.Message),
                NotFoundError notFound => Error.NotFound(notFound.Message),
                InternalError internalError => Error.InternalError(internalError.Message),
                _ => Error.InternalError(error.Message)
            },
            _ => TypedResults.NoContent()
        );

    protected static class Error
    {
        public static NotFound<ErrorResponseModel> NotFound(string message = "Resource not found.") =>
            TypedResults.NotFound(new ErrorResponseModel(message));

        public static UnauthorizedHttpResult Unauthorized() =>
            TypedResults.Unauthorized();

        public static BadRequest<ErrorResponseModel> BadRequest(string message) =>
            TypedResults.BadRequest(new ErrorResponseModel(message));

        public static JsonHttpResult<ErrorResponseModel> InternalError(
            string message = "Something went wrong with your request. Please contact support for assistance.") =>
            TypedResults.Json(
                new ErrorResponseModel(message),
                statusCode: StatusCodes.Status500InternalServerError);
    }
}
