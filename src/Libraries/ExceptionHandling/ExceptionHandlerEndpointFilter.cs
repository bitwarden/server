using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.ExceptionHandling;

/// <summary>
/// An <see cref="IEndpointFilter"/> that translates thrown exceptions into Bitwarden's
/// <see cref="ErrorResponseModel"/> with the same HTTP status codes that
/// <c>ExceptionHandlerFilterAttribute</c> produces for MVC controllers.
/// <see cref="AggregateException"/> is not handled and falls through to the default 500 branch.
/// </summary>
internal sealed class ExceptionHandlerEndpointFilter : IEndpointFilter
{
    private readonly ILogger<ExceptionHandlerEndpointFilter> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlerEndpointFilter(
        ILogger<ExceptionHandlerEndpointFilter> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception)
        {
            return Handle(exception);
        }
    }

    private IResult Handle(Exception exception)
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
            case UnauthorizedAccessException:
                message = "Unauthorized.";
                statusCode = StatusCodes.Status401Unauthorized;
                break;
            case ConflictException:
                message = exception.Message;
                statusCode = StatusCodes.Status409Conflict;
                break;
            default:
                _logger.LogError(0, exception, "Unhandled exception");
                message = "An unhandled server error has occurred.";
                statusCode = StatusCodes.Status500InternalServerError;
                break;
        }

        var errorModel = validationModel ?? new ErrorResponseModel(message);
        if (_environment.IsDevelopment())
        {
            errorModel.ExceptionMessage = exception.Message;
            errorModel.ExceptionStackTrace = exception.StackTrace;
            errorModel.InnerExceptionMessage = exception.InnerException?.Message;
        }

        return Results.Json(errorModel, statusCode: statusCode);
    }
}
