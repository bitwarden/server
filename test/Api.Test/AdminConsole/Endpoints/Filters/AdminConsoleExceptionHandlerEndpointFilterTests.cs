using Bit.Api.AdminConsole.Endpoints.Filters;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Endpoints.Filters;

public class AdminConsoleExceptionHandlerEndpointFilterTests
{
    private static EndpointFilterInvocationContext BuildContext(HttpContext? httpContext = null)
    {
        httpContext ??= BuildHttpContextWithLogger();
        var context = Substitute.For<EndpointFilterInvocationContext>();
        context.HttpContext.Returns(httpContext);
        return context;
    }

    /// <summary>
    /// The filter's fallback branch pulls an ILogger out of HttpContext.RequestServices; give it a real service
    /// provider so that resolution works even when the test doesn't otherwise care about logging.
    /// </summary>
    private static DefaultHttpContext BuildHttpContextWithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_ReturnsNotFoundResultWithErrorResponseModel()
    {
        var filter = new AdminConsoleExceptionHandlerEndpointFilter();
        var context = BuildContext();

        EndpointFilterDelegate next = _ => throw new NotFoundException();

        var result = await filter.InvokeAsync(context, next);

        var notFound = Assert.IsType<NotFound<ErrorResponseModel>>(result);
        Assert.NotNull(notFound.Value);
        Assert.Equal("Resource not found.", notFound.Value!.Message);
    }

    [Fact]
    public async Task InvokeAsync_BadRequestException_ReturnsBadRequestResultWithExceptionMessage()
    {
        var filter = new AdminConsoleExceptionHandlerEndpointFilter();
        var context = BuildContext();

        EndpointFilterDelegate next = _ => throw new BadRequestException("Bad input.");

        var result = await filter.InvokeAsync(context, next);

        var badRequest = Assert.IsType<BadRequest<ErrorResponseModel>>(result);
        Assert.NotNull(badRequest.Value);
        Assert.Equal("Bad input.", badRequest.Value!.Message);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500WithGenericErrorAndLogs()
    {
        var filter = new AdminConsoleExceptionHandlerEndpointFilter();

        // Wire a substitute logger so we can assert that unhandled exceptions get logged.
        var logger = Substitute.For<ILogger<AdminConsoleExceptionHandlerEndpointFilter>>();
        var services = new ServiceCollection();
        services.AddSingleton(logger);
        services.AddLogging();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        var context = BuildContext(httpContext);

        var thrown = new InvalidOperationException("boom");
        EndpointFilterDelegate next = _ => throw thrown;

        var result = await filter.InvokeAsync(context, next);

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, jsonResult.StatusCode);
        Assert.NotNull(jsonResult.Value);
        Assert.Equal("An error has occurred.", jsonResult.Value!.Message);

        // The filter must log unhandled exceptions with the underlying exception attached.
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            thrown,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThroughNextResult()
    {
        var filter = new AdminConsoleExceptionHandlerEndpointFilter();
        var context = BuildContext();
        var expected = TypedResults.NoContent();

        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expected);

        var result = await filter.InvokeAsync(context, next);

        Assert.Same(expected, result);
    }
}
