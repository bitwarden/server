using System.Diagnostics;
using Bit.Identity.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;
using IdentityServerEndpoint = Duende.IdentityServer.Hosting.Endpoint;

namespace Bit.Identity.Test.Utilities;

public class IdentityServerRouteDiagnosticsMiddlewareTests
{
    private static readonly IdentityServerEndpoint[] _endpoints =
    [
        new("Token", "/connect/token", typeof(object)),
        new("Discovery", "/.well-known/openid-configuration", typeof(object)),
    ];

    [Fact]
    public async Task Invoke_HandledProtocolRequest_TagsActivityAndMetrics()
    {
        var (context, activity, metricsTags) = CreateContext("/connect/token");
        var sut = CreateSut(_ => Task.CompletedTask);

        await sut.Invoke(context);

        Assert.Equal("/connect/token", activity.GetTagItem("http.route"));
        var tag = Assert.Single(metricsTags.Tags);
        Assert.Equal(new KeyValuePair<string, object?>("http.route", "/connect/token"), tag);
    }

    [Fact]
    public async Task Invoke_RequestPathCasingDiffers_UsesRegisteredPath()
    {
        var (context, activity, metricsTags) = CreateContext("/Connect/TOKEN");
        var sut = CreateSut(_ => Task.CompletedTask);

        await sut.Invoke(context);

        Assert.Equal("/connect/token", activity.GetTagItem("http.route"));
        Assert.Equal("/connect/token", Assert.Single(metricsTags.Tags).Value);
    }

    [Fact]
    public async Task Invoke_NonProtocolRequest_DoesNotTag()
    {
        var (context, activity, metricsTags) = CreateContext("/accounts/prelogin");
        var nextCalled = false;
        var sut = CreateSut(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.Invoke(context);

        Assert.True(nextCalled);
        Assert.Null(activity.GetTagItem("http.route"));
        Assert.Empty(metricsTags.Tags);
    }

    [Fact]
    public async Task Invoke_RequestNotHandledByIdentityServer_DoesNotTag()
    {
        var (context, activity, metricsTags) = CreateContext("/connect/token");
        var sut = CreateSut(ctx => IdentityServerRouteDiagnosticsMiddleware.MarkUnhandled(ctx, _ => Task.CompletedTask));

        await sut.Invoke(context);

        Assert.Null(activity.GetTagItem("http.route"));
        Assert.Empty(metricsTags.Tags);
    }

    [Fact]
    public async Task Invoke_NoDiagnosticsFeatures_DoesNotThrow()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/connect/token";
        var sut = CreateSut(_ => Task.CompletedTask);

        await sut.Invoke(context);
    }

    private static IdentityServerRouteDiagnosticsMiddleware CreateSut(RequestDelegate next)
        => new(next, _endpoints);

    private static (HttpContext Context, Activity Activity, TestHttpMetricsTagsFeature MetricsTags) CreateContext(string path)
    {
        var activity = new Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn");
        var metricsTags = new TestHttpMetricsTagsFeature();

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Features.Set<IHttpActivityFeature>(new TestHttpActivityFeature(activity));
        context.Features.Set<IHttpMetricsTagsFeature>(metricsTags);

        return (context, activity, metricsTags);
    }

    private sealed class TestHttpActivityFeature(Activity activity) : IHttpActivityFeature
    {
        public Activity Activity { get; set; } = activity;
    }

    private sealed class TestHttpMetricsTagsFeature : IHttpMetricsTagsFeature
    {
        public ICollection<KeyValuePair<string, object?>> Tags { get; } = [];
        public bool MetricsDisabled { get; set; }
    }
}
