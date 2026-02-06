using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

public abstract class BaseAdminConsoleController : Controller
{
    protected static IResult Handle(CommandResult commandResult) =>
        commandResult.Match<IResult>(
            error => error switch
            {
                BadRequestError badRequest => TypedResults.BadRequest(new ErrorResponseModel(badRequest.Message)),
                NotFoundError notFound => TypedResults.NotFound(new ErrorResponseModel(notFound.Message)),
                InternalError internalError => TypedResults.Json(
                    new ErrorResponseModel(internalError.Message),
                    statusCode: StatusCodes.Status500InternalServerError),
                _ => TypedResults.Json(
                    new ErrorResponseModel(error.Message),
                    statusCode: StatusCodes.Status500InternalServerError
                )
            },
            _ => TypedResults.NoContent()
        );
}
