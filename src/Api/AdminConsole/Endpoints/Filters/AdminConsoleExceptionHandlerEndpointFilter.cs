using Bit.Core.Exceptions;

namespace Bit.Api.AdminConsole.Endpoints.Filters;

/// <summary>
/// Turns exceptions thrown by admin console Minimal API endpoints into RFC 7807 problem responses.
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
            return TypedResults.Problem(title: "Resource not found.", statusCode: StatusCodes.Status404NotFound);
        }
        catch (Exception exception)
        {
            var endpointName = context.HttpContext.GetEndpoint()?.DisplayName;
            context.HttpContext.RequestServices.GetRequiredService<ILogger<AdminConsoleExceptionHandlerEndpointFilter>>()
                .LogError(exception, "Unhandled exception in {EndpointName}", endpointName);
            return TypedResults.Problem(
                title: "An error has occurred.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
