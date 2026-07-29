using Bit.Api.AdminConsole.Endpoints.Filters;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Endpoints.Filters;

public class AdminConsoleExceptionHandlerEndpointFilterTests
{
    [Fact]
    public async Task InvokeAsync_NextSucceeds_ReturnsNextResult()
    {
        var context = CreateContext();
        var expected = TypedResults.NoContent();

        var result = await new AdminConsoleExceptionHandlerEndpointFilter().InvokeAsync(context, _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_NextThrowsNotFoundException_ReturnsNotFound()
    {
        var context = CreateContext();

        var result = await new AdminConsoleExceptionHandlerEndpointFilter().InvokeAsync(
            context, _ => throw new NotFoundException());

        var notFound = Assert.IsType<NotFound<ErrorResponseModel>>(result);
        Assert.Equal("Resource not found.", notFound.Value.Message);
    }

    [Fact]
    public async Task InvokeAsync_NextThrowsBadRequestException_ReturnsBadRequest()
    {
        var context = CreateContext();

        var result = await new AdminConsoleExceptionHandlerEndpointFilter().InvokeAsync(
            context, _ => throw new BadRequestException("Requested collections must belong to the same organization."));

        var badRequest = Assert.IsType<BadRequest<ErrorResponseModel>>(result);
        Assert.Equal("Requested collections must belong to the same organization.", badRequest.Value.Message);
    }

    [Fact]
    public async Task InvokeAsync_NextThrowsUnexpectedException_ReturnsInternalServerErrorAndLogs()
    {
        var context = CreateContext("PatchCollectionUserAccess");

        var result = await new AdminConsoleExceptionHandlerEndpointFilter().InvokeAsync(
            context, _ => throw new InvalidOperationException());

        var json = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, json.StatusCode);
        Assert.Equal("An error has occurred.", json.Value.Message);
    }

    private static EndpointFilterInvocationContext CreateContext(string? endpointDisplayName = null)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        if (endpointDisplayName != null)
        {
            httpContext.SetEndpoint(new Endpoint(null, EndpointMetadataCollection.Empty, endpointDisplayName));
        }

        return EndpointFilterInvocationContext.Create(httpContext);
    }
}
