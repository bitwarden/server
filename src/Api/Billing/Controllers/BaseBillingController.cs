using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

public abstract class BaseBillingController : Controller
{
    protected static class Error
    {
        public static BadRequest<ErrorResponseModel> BadRequest(Dictionary<string, IEnumerable<string>> errors) =>
            TypedResults.BadRequest(new ErrorResponseModel(errors));

        public static BadRequest<ErrorResponseModel> BadRequest(string message) =>
            TypedResults.BadRequest(new ErrorResponseModel(message));

        public static NotFound<ErrorResponseModel> NotFound() =>
            TypedResults.NotFound(new ErrorResponseModel("Resource not found."));

        public static JsonHttpResult<ErrorResponseModel> ServerError(string message = "Something went wrong with your request. Please contact support.") =>
            TypedResults.Json(
                new ErrorResponseModel(message),
                statusCode: StatusCodes.Status500InternalServerError);

        public static JsonHttpResult<ErrorResponseModel> Unauthorized(string message = "Unauthorized.") =>
            TypedResults.Json(
                new ErrorResponseModel(message),
                statusCode: StatusCodes.Status401Unauthorized);
    }
}
