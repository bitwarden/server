using Bit.Commercial.Pam.Api.Endpoints.Filters;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Api.Endpoints.Filters;

public class RequireFeatureEndpointFilterTests
{
    private const string Flag = "pm-test-flag";

    [Fact]
    public async Task InvokeAsync_FeatureEnabled_CallsNext()
    {
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(Flag).Returns(true);
        var context = CreateContext(featureService);
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ok");
        };

        var result = await new RequireFeatureEndpointFilter(Flag).InvokeAsync(context, next);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task InvokeAsync_FeatureDisabled_ThrowsFeatureUnavailableAndSkipsNext()
    {
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(Flag).Returns(false);
        var context = CreateContext(featureService);
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ok");
        };

        await Assert.ThrowsAsync<FeatureUnavailableException>(
            async () => await new RequireFeatureEndpointFilter(Flag).InvokeAsync(context, next));
        Assert.False(nextCalled);
    }

    private static EndpointFilterInvocationContext CreateContext(IFeatureService featureService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(featureService);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return EndpointFilterInvocationContext.Create(httpContext);
    }
}
