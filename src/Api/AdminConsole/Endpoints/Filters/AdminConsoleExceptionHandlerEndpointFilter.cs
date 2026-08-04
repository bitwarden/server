using Bit.Core.Exceptions;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Endpoints.Filters;

/// <summary>
/// Turns exceptions thrown by admin console Minimal API endpoints into the same ErrorResponseModel shape
/// every other Bitwarden endpoint already returns.
/// </summary>
public class AdminConsoleExceptionHandlerEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (NotFoundException)
        {
            return TypedResults.NotFound(new ErrorResponseModel("Resource not found."));
        }
        catch (BadRequestException badRequestException)
        {
            return TypedResults.BadRequest(new ErrorResponseModel(badRequestException.Message));
        }
        catch (Exception exception)
        {
            var endpointName = context.HttpContext.GetEndpoint()?.DisplayName;
            context.HttpContext.RequestServices.GetRequiredService<ILogger<AdminConsoleExceptionHandlerEndpointFilter>>()
                .LogError(exception, "Unhandled exception in {EndpointName}", endpointName);
            return TypedResults.Json(
                new ErrorResponseModel("An error has occurred."), statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
