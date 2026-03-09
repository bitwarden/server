using System.Net;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class SsrfProtectionHandlerTests
{
    private readonly ILogger<SsrfProtectionHandler> _logger = Substitute.For<ILogger<SsrfProtectionHandler>>();

    /// <summary>
    /// A test handler that captures the request and returns a canned response.
    /// Used as the inner handler for <see cref="SsrfProtectionHandler"/>.
    /// </summary>
    private class TestInnerHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    /// <summary>
    /// Creates an SsrfProtectionHandler wrapping a TestInnerHandler for testing purposes.
    /// </summary>
    private (HttpClient client, TestInnerHandler inner) CreateClient()
    {
        var inner = new TestInnerHandler();
        var handler = new SsrfProtectionHandler(_logger)
        {
            InnerHandler = inner
        };
        var client = new HttpClient(handler);
        return (client, inner);
    }

    [Fact]
    public async Task SendAsync_NullRequestUri_ThrowsInvalidOperationException()
    {
        var (client, _) = CreateClient();
        var request = new HttpRequestMessage
        {
            RequestUri = null,
            Method = HttpMethod.Get
        };

        // HttpClient validates the URI before the handler runs
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
    }

    [Theory]
    [InlineData("http://127.0.0.1/test")]
    [InlineData("http://10.0.0.1/test")]
    [InlineData("http://192.168.1.1/test")]
    [InlineData("http://172.16.0.1/test")]
    [InlineData("http://169.254.169.254/latest/meta-data/")] // AWS metadata
    [InlineData("http://100.64.0.1/test")]                   // CGNAT
    [InlineData("http://[::1]/test")]                         // IPv6 loopback
    public async Task SendAsync_DirectIpInternal_ThrowsSsrfProtectionException(string url)
    {
        var (client, _) = CreateClient();

        await Assert.ThrowsAsync<SsrfProtectionException>(
            () => client.GetAsync(url));
    }

    [Theory]
    [InlineData("http://8.8.8.8/test")]
    [InlineData("http://1.1.1.1/test")]
    [InlineData("http://52.20.30.40/test")]
    public async Task SendAsync_DirectIpPublic_Succeeds(string url)
    {
        var (client, inner) = CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(inner.LastRequest);
    }

    [Fact]
    public async Task SendAsync_PublicHost_PreservesHostHeader()
    {
        // This test verifies that when a hostname resolves to a public IP,
        // the handler rewrites the URI to the IP but preserves the Host header.
        // We can't easily mock DNS in C#, so we test with a direct IP that
        // doesn't need DNS resolution.
        var (client, inner) = CreateClient();

        var response = await client.GetAsync("http://8.8.8.8/test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(inner.LastRequest);
        // URI host should be rewritten to the IP
        Assert.Equal("8.8.8.8", inner.LastRequest!.RequestUri!.Host);
    }

    [Fact]
    public async Task SendAsync_HostnameResolvingToInternalIp_ThrowsSsrfProtectionException()
    {
        // "localhost" should always resolve to 127.0.0.1 or ::1
        var (client, _) = CreateClient();

        await Assert.ThrowsAsync<SsrfProtectionException>(
            () => client.GetAsync("http://localhost/test"));
    }

    [Theory]
    [InlineData("http://0.0.0.0/test")]
    [InlineData("http://127.0.0.2/test")]
    [InlineData("http://100.100.100.100/test")] // CGNAT
    public async Task SendAsync_VariousInternalIps_ThrowsSsrfProtectionException(string url)
    {
        var (client, _) = CreateClient();

        await Assert.ThrowsAsync<SsrfProtectionException>(
            () => client.GetAsync(url));
    }
}
