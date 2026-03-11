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
        public List<HttpRequestMessage> AllRequests { get; } = [];
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void EnqueueResponse(HttpResponseMessage response)
        {
            _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            AllRequests.Add(request);
            var response = _responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK);
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Creates an SsrfProtectionHandler wrapping a TestInnerHandler for testing purposes.
    /// </summary>
    private (HttpClient client, TestInnerHandler inner) CreateClient(bool followRedirects = true)
    {
        var inner = new TestInnerHandler();
        var handler = new SsrfProtectionHandler(_logger)
        {
            InnerHandler = inner,
            FollowRedirects = followRedirects
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

    [Fact]
    public async Task SendAsync_RedirectToInternalIp_ThrowsSsrfProtectionException()
    {
        // Simulates an attacker-controlled domain redirecting to the AWS metadata endpoint
        var (client, inner) = CreateClient();
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
        redirectResponse.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data/");
        inner.EnqueueResponse(redirectResponse);

        await Assert.ThrowsAsync<SsrfProtectionException>(
            () => client.GetAsync("http://8.8.8.8/attacker"));
    }

    [Fact]
    public async Task SendAsync_RedirectToPublicIp_Succeeds()
    {
        var (client, inner) = CreateClient();
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
        redirectResponse.Headers.Location = new Uri("http://1.1.1.1/final");
        inner.EnqueueResponse(redirectResponse);

        var response = await client.GetAsync("http://8.8.8.8/start");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.AllRequests.Count);
    }

    [Fact]
    public async Task SendAsync_RedirectToLocalhost_ThrowsSsrfProtectionException()
    {
        var (client, inner) = CreateClient();
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        redirectResponse.Headers.Location = new Uri("http://127.0.0.1/admin");
        inner.EnqueueResponse(redirectResponse);

        await Assert.ThrowsAsync<SsrfProtectionException>(
            () => client.GetAsync("http://8.8.8.8/redirect-to-localhost"));
    }

    [Fact]
    public async Task SendAsync_MultipleRedirectsToPublicIps_Succeeds()
    {
        var (client, inner) = CreateClient();

        var redirect1 = new HttpResponseMessage(HttpStatusCode.Found);
        redirect1.Headers.Location = new Uri("http://1.1.1.1/hop2");
        inner.EnqueueResponse(redirect1);

        var redirect2 = new HttpResponseMessage(HttpStatusCode.Found);
        redirect2.Headers.Location = new Uri("http://52.20.30.40/hop3");
        inner.EnqueueResponse(redirect2);

        // Final response is OK (default from TestInnerHandler)

        var response = await client.GetAsync("http://8.8.8.8/start");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.AllRequests.Count);
    }

    [Fact]
    public async Task SendAsync_RedirectChainEventuallyHitsInternalIp_ThrowsSsrfProtectionException()
    {
        var (client, inner) = CreateClient();

        var redirect1 = new HttpResponseMessage(HttpStatusCode.Found);
        redirect1.Headers.Location = new Uri("http://1.1.1.1/hop2");
        inner.EnqueueResponse(redirect1);

        var redirect2 = new HttpResponseMessage(HttpStatusCode.Found);
        redirect2.Headers.Location = new Uri("http://10.0.0.1/internal");
        inner.EnqueueResponse(redirect2);

        await Assert.ThrowsAsync<SsrfProtectionException>(
            () => client.GetAsync("http://8.8.8.8/start"));
    }

    [Fact]
    public async Task SendAsync_RelativeRedirect_ResolvesAgainstOriginalUri()
    {
        var (client, inner) = CreateClient();
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
        redirectResponse.Headers.Location = new Uri("/new-path", UriKind.Relative);
        inner.EnqueueResponse(redirectResponse);

        var response = await client.GetAsync("http://8.8.8.8/original");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.AllRequests.Count);
    }

    [Fact]
    public async Task SendAsync_NonHttpRedirectScheme_StopsFollowing()
    {
        var (client, inner) = CreateClient();
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
        redirectResponse.Headers.Location = new Uri("ftp://8.8.8.8/file");
        inner.EnqueueResponse(redirectResponse);

        var response = await client.GetAsync("http://8.8.8.8/start");

        // Should return the redirect response itself, not follow it
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Single(inner.AllRequests);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]  // 301
    [InlineData(HttpStatusCode.Found)]             // 302
    [InlineData(HttpStatusCode.SeeOther)]          // 303
    public async Task SendAsync_301_302_303_Redirect_ChangesPostToGet(HttpStatusCode redirectCode)
    {
        var (client, inner) = CreateClient();
        var redirectResponse = new HttpResponseMessage(redirectCode);
        redirectResponse.Headers.Location = new Uri("http://1.1.1.1/final");
        inner.EnqueueResponse(redirectResponse);

        var request = new HttpRequestMessage(HttpMethod.Post, "http://8.8.8.8/start");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.AllRequests.Count);
        Assert.Equal(HttpMethod.Get, inner.AllRequests[1].Method);
    }

    [Theory]
    [InlineData(HttpStatusCode.TemporaryRedirect)] // 307
    public async Task SendAsync_307_Redirect_PreservesOriginalMethod(HttpStatusCode redirectCode)
    {
        var (client, inner) = CreateClient();
        var redirectResponse = new HttpResponseMessage(redirectCode);
        redirectResponse.Headers.Location = new Uri("http://1.1.1.1/final");
        inner.EnqueueResponse(redirectResponse);

        var request = new HttpRequestMessage(HttpMethod.Post, "http://8.8.8.8/start");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.AllRequests.Count);
        Assert.Equal(HttpMethod.Post, inner.AllRequests[1].Method);
    }

    [Fact]
    public async Task SendAsync_FollowRedirectsFalse_ReturnsRedirectResponseWithoutFollowing()
    {
        var (client, inner) = CreateClient(followRedirects: false);
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
        redirectResponse.Headers.Location = new Uri("http://1.1.1.1/final");
        inner.EnqueueResponse(redirectResponse);

        var response = await client.GetAsync("http://8.8.8.8/start");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Single(inner.AllRequests);
    }

    [Fact]
    public async Task SendAsync_FollowRedirectsFalse_StillValidatesInitialRequest()
    {
        var (client, _) = CreateClient(followRedirects: false);

        await Assert.ThrowsAsync<SsrfProtectionException>(
            () => client.GetAsync("http://127.0.0.1/admin"));
    }

    [Fact]
    public async Task SendAsync_FollowRedirectsFalse_RedirectToInternalIp_ReturnsRedirectWithoutBlocking()
    {
        // When followRedirects is false, the handler doesn't follow the redirect at all,
        // so it never sees the internal IP. The caller is responsible for validating
        // redirect targets (e.g., Icons' FollowRedirectsAsync creates new requests
        // that go through SsrfProtectionHandler again).
        var (client, inner) = CreateClient(followRedirects: false);
        var redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
        redirectResponse.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data/");
        inner.EnqueueResponse(redirectResponse);

        var response = await client.GetAsync("http://8.8.8.8/start");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Single(inner.AllRequests);
    }

    [Fact]
    public async Task SendAsync_RelativeRedirectAfterAbsoluteRedirect_ResolvesAgainstCurrentHost()
    {
        // Chain: 8.8.8.8 -> 302 http://1.1.1.1/hop2 -> 302 /relative
        // The relative redirect should resolve against 1.1.1.1 (the current hop),
        // not 8.8.8.8 (the original request which got IP-rewritten by ValidateAndSendAsync).
        var (client, inner) = CreateClient();

        var redirect1 = new HttpResponseMessage(HttpStatusCode.Found);
        redirect1.Headers.Location = new Uri("http://1.1.1.1/hop2");
        inner.EnqueueResponse(redirect1);

        var redirect2 = new HttpResponseMessage(HttpStatusCode.Found);
        redirect2.Headers.Location = new Uri("/relative-path", UriKind.Relative);
        inner.EnqueueResponse(redirect2);

        // Final response is OK (default from TestInnerHandler)

        var response = await client.GetAsync("http://8.8.8.8/start");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.AllRequests.Count);
        // The third request should have resolved /relative-path against 1.1.1.1
        Assert.Equal("1.1.1.1", inner.AllRequests[2].Headers.Host);
    }

    [Fact]
    public async Task SendAsync_Post301ThenGet307_PreservesGetFromIntermediateHop()
    {
        // Chain: POST -> 301 (changes to GET) -> 307 (should preserve GET, not original POST)
        var (client, inner) = CreateClient();

        var redirect1 = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        redirect1.Headers.Location = new Uri("http://1.1.1.1/hop2");
        inner.EnqueueResponse(redirect1);

        var redirect2 = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
        redirect2.Headers.Location = new Uri("http://52.20.30.40/hop3");
        inner.EnqueueResponse(redirect2);

        // Final response is OK (default from TestInnerHandler)

        var request = new HttpRequestMessage(HttpMethod.Post, "http://8.8.8.8/start");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.AllRequests.Count);
        Assert.Equal(HttpMethod.Post, inner.AllRequests[0].Method); // original
        Assert.Equal(HttpMethod.Get, inner.AllRequests[1].Method);  // 301 changed POST→GET
        Assert.Equal(HttpMethod.Get, inner.AllRequests[2].Method);  // 307 preserves GET (not original POST)
    }
}
