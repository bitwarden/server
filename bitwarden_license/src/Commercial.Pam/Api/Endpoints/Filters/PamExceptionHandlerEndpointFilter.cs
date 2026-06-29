using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Commercial.Pam.Api.Endpoints.Filters;

/// <summary>
/// Minimal API equivalent of the internal-API branch of <c>Bit.Api.Utilities.ExceptionHandlerFilterAttribute</c>.
/// Minimal API endpoints do not run MVC exception filters and <c>src/Api</c> has no exception-handling middleware,
/// so this filter translates thrown exceptions into Bitwarden's <see cref="ErrorResponseModel"/> with the same
/// status codes the controllers produced (e.g. <see cref="NotFoundException"/> → 404 "Resource not found.").
/// </summary>
public class PamExceptionHandlerEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception)
        {
            return Handle(exception, context.HttpContext);
        }
    }

    private static IResult Handle(Exception exception, HttpContext httpContext)
    {
        var message = "An error has occurred.";
        int statusCode;
        ErrorResponseModel? validationModel = null;

        switch (exception)
        {
            case BadRequestException badRequestException:
                statusCode = StatusCodes.Status400BadRequest;
                if (badRequestException.ModelState != null)
                {
                    validationModel = new ErrorResponseModel(badRequestException.ModelState);
                }
                else
                {
                    message = badRequestException.Message;
                }
                break;
            case NotSupportedException when !string.IsNullOrWhiteSpace(exception.Message):
                message = exception.Message;
                statusCode = StatusCodes.Status400BadRequest;
                break;
            case ApplicationException:
                statusCode = StatusCodes.Status402PaymentRequired;
                break;
            case NotFoundException:
                message = "Resource not found.";
                statusCode = StatusCodes.Status404NotFound;
                break;
            case SecurityTokenValidationException:
                message = "Invalid token.";
                statusCode = StatusCodes.Status403Forbidden;
                break;
            case UnauthorizedAccessException:
                message = "Unauthorized.";
                statusCode = StatusCodes.Status401Unauthorized;
                break;
            case ConflictException:
                message = exception.Message;
                statusCode = StatusCodes.Status409Conflict;
                break;
            case AggregateException aggregateException:
                statusCode = StatusCodes.Status400BadRequest;
                validationModel = new ErrorResponseModel(message, aggregateException.InnerExceptions.Select(e => e.Message));
                break;
            default:
                httpContext.RequestServices.GetRequiredService<ILogger<PamExceptionHandlerEndpointFilter>>()
                    .LogError(0, exception, "Unhandled exception");
                message = "An unhandled server error has occurred.";
                statusCode = StatusCodes.Status500InternalServerError;
                break;
        }

        var errorModel = validationModel ?? new ErrorResponseModel(message);
        var environment = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (environment.IsDevelopment())
        {
            errorModel.ExceptionMessage = exception.Message;
            errorModel.ExceptionStackTrace = exception.StackTrace;
            errorModel.InnerExceptionMessage = exception.InnerException?.Message;
        }

        return Results.Json(errorModel, statusCode: statusCode);
    }
}
