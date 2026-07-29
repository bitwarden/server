using Bit.Api.AdminConsole.Endpoints.Filters;
using Bit.Core.Exceptions;
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
    public async Task InvokeAsync_NextThrowsNotFoundException_ReturnsNotFoundProblem()
    {
        var context = CreateContext();

        var result = await new AdminConsoleExceptionHandlerEndpointFilter().InvokeAsync(
            context, _ => throw new NotFoundException());

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Equal("Resource not found.", problem.ProblemDetails.Title);
    }

    [Fact]
    public async Task InvokeAsync_NextThrowsUnexpectedException_ReturnsInternalServerErrorProblemAndLogs()
    {
        var context = CreateContext("PatchCollectionUserAccess");

        var result = await new AdminConsoleExceptionHandlerEndpointFilter().InvokeAsync(
            context, _ => throw new InvalidOperationException());

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Equal("An error has occurred.", problem.ProblemDetails.Title);
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
