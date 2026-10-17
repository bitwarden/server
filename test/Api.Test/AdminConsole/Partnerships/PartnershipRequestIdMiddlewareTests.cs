using Bit.Api.AdminConsole.Partnerships;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships;

public class PartnershipRequestIdMiddlewareTests
{
    [Theory]
    [InlineData("/partnerships/v1")]
    [InlineData("/partnerships/v1/entitlements")]
    [InlineData("/Partnerships/V1/entitlements")]
    public async Task InvokeAsync_PartnershipPath_SetsRequestIdHeader(string path)
    {
        var context = CreateContext(path);

        await new PartnershipRequestIdMiddleware(Substitute.For<RequestDelegate>()).InvokeAsync(context);

        Assert.Equal("test-request-id", context.Response.Headers["X-Request-Id"]);
    }

    [Theory]
    [InlineData("/organizations")]
    [InlineData("/partnerships")]
    [InlineData("/partnerships/v10")]
    public async Task InvokeAsync_OtherPath_DoesNotSetRequestIdHeader(string path)
    {
        var context = CreateContext(path);

        await new PartnershipRequestIdMiddleware(Substitute.For<RequestDelegate>()).InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Request-Id"));
    }

    [Fact]
    public async Task InvokeAsync_CallsNext()
    {
        var next = Substitute.For<RequestDelegate>();
        var context = CreateContext("/partnerships/v1/entitlements");

        await new PartnershipRequestIdMiddleware(next).InvokeAsync(context);

        await next.Received(1).Invoke(context);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "test-request-id" };
        context.Request.Path = path;
        return context;
    }
}
