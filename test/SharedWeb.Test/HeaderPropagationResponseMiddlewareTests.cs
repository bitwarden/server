using System.Diagnostics;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace SharedWeb.Test;

public class HeaderPropagationResponseMiddlewareTests
{
    private readonly RequestDelegate _next;

    public HeaderPropagationResponseMiddlewareTests()
    {
        _next = Substitute.For<RequestDelegate>();
    }

    [Fact]
    public async Task InvokeAsync_WithMatchingHeader_SetsRoutedResponseHeader()
    {
        var middleware = new HeaderPropagationResponseMiddleware(_next, ["X-Canary"]);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Canary"] = "true";

        await middleware.InvokeAsync(context);

        Assert.Equal("true", context.Response.Headers["X-Canary-Routed"]);
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithoutHeader_DoesNotSetRoutedResponseHeader()
    {
        var middleware = new HeaderPropagationResponseMiddleware(_next, ["X-Canary"]);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Canary-Routed"));
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithHeaderValueNotTrue_DoesNotSetRoutedResponseHeader()
    {
        var middleware = new HeaderPropagationResponseMiddleware(_next, ["X-Canary"]);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Canary"] = "false";

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Canary-Routed"));
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithHeaderValueTrue_CaseInsensitive()
    {
        var middleware = new HeaderPropagationResponseMiddleware(_next, ["X-Canary"]);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Canary"] = "TRUE";

        await middleware.InvokeAsync(context);

        Assert.Equal("true", context.Response.Headers["X-Canary-Routed"]);
    }

    [Fact]
    public async Task InvokeAsync_WithMultipleConfiguredHeaders_SetsMatchingRoutedHeaders()
    {
        var middleware = new HeaderPropagationResponseMiddleware(_next, ["X-Canary", "X-Preview"]);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Canary"] = "true";

        await middleware.InvokeAsync(context);

        Assert.Equal("true", context.Response.Headers["X-Canary-Routed"]);
        Assert.False(context.Response.Headers.ContainsKey("X-Preview-Routed"));
    }

    [Fact]
    public async Task InvokeAsync_WithNoConfiguredHeaders_CallsNext()
    {
        var middleware = new HeaderPropagationResponseMiddleware(_next, []);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Canary-Routed"));
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithActivity_SetsBaselineTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("test");
        using var activity = source.StartActivity("test-operation");

        var middleware = new HeaderPropagationResponseMiddleware(_next, []);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal("unknown", activity?.GetTagItem("pod.name"));
        Assert.NotNull(activity?.GetTagItem("build.hash"));
    }

    [Fact]
    public async Task InvokeAsync_WithActivityAndMatchingHeader_SetsCanaryTag()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("test");
        using var activity = source.StartActivity("test-operation");

        var middleware = new HeaderPropagationResponseMiddleware(_next, ["X-Canary"]);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Canary"] = "true";

        await middleware.InvokeAsync(context);

        Assert.Equal("true", activity?.GetTagItem("canary"));
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        var middleware = new HeaderPropagationResponseMiddleware(_next, ["X-Canary"]);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Canary"] = "true";

        await middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }
}
