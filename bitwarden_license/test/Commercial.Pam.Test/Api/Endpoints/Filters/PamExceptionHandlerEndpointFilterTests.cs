using Bit.Commercial.Pam.Api.Endpoints.Filters;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Api.Endpoints.Filters;

public class PamExceptionHandlerEndpointFilterTests
{
    [Fact]
    public async Task InvokeAsync_NoException_PassesResultThrough()
    {
        var context = CreateContext();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("ok");

        var result = await new PamExceptionHandlerEndpointFilter().InvokeAsync(context, next);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404ErrorResponseModel()
    {
        var context = CreateContext();
        EndpointFilterDelegate next = _ => throw new NotFoundException();

        var result = await new PamExceptionHandlerEndpointFilter().InvokeAsync(context, next);

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status404NotFound, jsonResult.StatusCode);
        Assert.Equal("Resource not found.", jsonResult.Value!.Message);
    }

    [Fact]
    public async Task InvokeAsync_FeatureUnavailableException_Returns404()
    {
        // FeatureUnavailableException derives from NotFoundException, so the feature gate maps to 404 like the rest.
        var context = CreateContext();
        EndpointFilterDelegate next = _ => throw new FeatureUnavailableException();

        var result = await new PamExceptionHandlerEndpointFilter().InvokeAsync(context, next);

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status404NotFound, jsonResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_BadRequestExceptionWithModelState_Returns400WithValidationErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Required.");
        var context = CreateContext();
        EndpointFilterDelegate next = _ => throw new BadRequestException(modelState);

        var result = await new PamExceptionHandlerEndpointFilter().InvokeAsync(context, next);

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.True(jsonResult.Value!.ValidationErrors!.ContainsKey("Name"));
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        var context = CreateContext();
        EndpointFilterDelegate next = _ => throw new InvalidOperationException("boom");

        var result = await new PamExceptionHandlerEndpointFilter().InvokeAsync(context, next);

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, jsonResult.StatusCode);
    }

    private static EndpointFilterInvocationContext CreateContext()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        var services = new ServiceCollection();
        services.AddSingleton(environment);
        services.AddLogging();
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return EndpointFilterInvocationContext.Create(httpContext);
    }
}
