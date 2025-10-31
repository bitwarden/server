using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

public abstract class BaseAdminConsoleController : Controller
{
    protected static IResult Handle<T>(CommandResult<T> commandResult) =>
        commandResult.Match<IResult>(
            error => error switch
            {
                BadRequestError badRequest => TypedResults.BadRequest(new ErrorResponseModel(badRequest.Message)),
                NotFoundError notFound => TypedResults.NotFound(new ErrorResponseModel(notFound.Message)),
                InternalError internalError => TypedResults.Json(
                    new ErrorResponseModel(internalError.Message),
                    statusCode: StatusCodes.Status500InternalServerError),
                _ => TypedResults.Json(
                    new ErrorResponseModel(commandResult.AsError.Message),
                    statusCode: StatusCodes.Status500InternalServerError
                )
            },
            _ => TypedResults.NoContent()
        );
}
