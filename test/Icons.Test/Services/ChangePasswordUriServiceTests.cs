using System.Net;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;
using Xunit;

namespace Bit.Icons.Test.Services;

public class ChangePasswordUriServiceTests : ServiceTestBase<ChangePasswordUriService>
{
    private static readonly IPAddress _publicIp = IPAddress.Parse("93.184.216.34");
    private static readonly IPAddress _loopbackIp = IPAddress.Parse("127.0.0.1");

    /// <summary>
    /// A fake IUriService that resolves all URIs to the given IP address.
    /// </summary>
    private class FakeUriService : IUriService
    {
        private readonly IPAddress _ip;
        private readonly bool _shouldSucceed;

        public FakeUriService(IPAddress ip, bool shouldSucceed = true)
        {
            _ip = ip;
            _shouldSucceed = shouldSucceed;
        }

        public bool TryGetUri(string stringUri, out IconUri? iconUri)
        {
            if (!_shouldSucceed || !Uri.TryCreate(stringUri, UriKind.Absolute, out var uri))
            {
                iconUri = null;
                return false;
            }
            iconUri = new IconUri(uri, _ip);
            return true;
        }

        public bool TryGetUri(Uri uri, out IconUri? iconUri)
        {
            if (!_shouldSucceed)
            {
                iconUri = null;
                return false;
            }
            iconUri = new IconUri(uri, _ip);
            return true;
        }

        public bool TryGetRedirect(HttpResponseMessage response, IconUri originalUri, out IconUri? iconUri)
        {
            iconUri = null;
            return false;
        }
    }

    /// <summary>
    /// A fake IUriService that succeeds for initial requests but returns an internal IP for redirects.
    /// </summary>
    private class FakeUriServiceWithInternalRedirect : IUriService
    {
        private readonly IPAddress _initialIp;
        private readonly IPAddress _redirectIp;

        public FakeUriServiceWithInternalRedirect(IPAddress initialIp, IPAddress redirectIp)
        {
            _initialIp = initialIp;
            _redirectIp = redirectIp;
        }

        public bool TryGetUri(string stringUri, out IconUri? iconUri)
        {
            if (!Uri.TryCreate(stringUri, UriKind.Absolute, out var uri))
            {
                iconUri = null;
                return false;
            }
            iconUri = new IconUri(uri, _initialIp);
            return true;
        }

        public bool TryGetUri(Uri uri, out IconUri? iconUri)
        {
            iconUri = new IconUri(uri, _initialIp);
            return true;
        }

        public bool TryGetRedirect(HttpResponseMessage response, IconUri originalUri, out IconUri? iconUri)
        {
            if (response.Headers.Location == null)
            {
                iconUri = null;
                return false;
            }

            var redirectUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(originalUri.InnerUri, response.Headers.Location);

            iconUri = new IconUri(redirectUri, _redirectIp);
            return true;
        }
    }

    [Theory]
    [InlineData("https://example.com", "https://example.com:443/.well-known/change-password")]
    public async Task GetChangePasswordUri_WhenBothChecksPass_ReturnsWellKnownUrl(string domain, string expectedUrl)
    {
        // Arrange
        var mockedHandler = new MockedHttpMessageHandler();
        var uriService = new FakeUriService(_publicIp);

        // Match requests by path since the host will be the resolved IP
        mockedHandler
            .When(r => r.RequestUri!.AbsolutePath.Contains("resource-that-should-not-exist"))
            .RespondWith(HttpStatusCode.NotFound)
            .WithContent(new StringContent("Not found"));

        mockedHandler
            .When(r => r.RequestUri!.AbsolutePath == "/.well-known/change-password")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Ok"));

        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(mockedHandler.ToHttpClient());

        var service = new ChangePasswordUriService(mockHttpFactory, uriService);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Equal(expectedUrl, result);
    }

    [Theory]
    [InlineData("https://example.com")]
    public async Task GetChangePasswordUri_WhenResourceThatShouldNotExistReturns200_ReturnsNull(string domain)
    {
        var mockedHandler = new MockedHttpMessageHandler();
        var uriService = new FakeUriService(_publicIp);

        mockedHandler
            .When(r => r.RequestUri!.AbsolutePath.Contains("resource-that-should-not-exist"))
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Ok"));

        mockedHandler
            .When(r => r.RequestUri!.AbsolutePath == "/.well-known/change-password")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Ok"));

        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(mockedHandler.ToHttpClient());

        var service = new ChangePasswordUriService(mockHttpFactory, uriService);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("https://example.com")]
    public async Task GetChangePasswordUri_WhenChangePasswordUrlNotFound_ReturnsNull(string domain)
    {
        var mockedHandler = new MockedHttpMessageHandler();
        var uriService = new FakeUriService(_publicIp);

        mockedHandler
            .When(r => r.RequestUri!.AbsolutePath.Contains("resource-that-should-not-exist"))
            .RespondWith(HttpStatusCode.NotFound)
            .WithContent(new StringContent("Not found"));

        mockedHandler
            .When(r => r.RequestUri!.AbsolutePath == "/.well-known/change-password")
            .RespondWith(HttpStatusCode.NotFound)
            .WithContent(new StringContent("Not found"));

        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(mockedHandler.ToHttpClient());

        var service = new ChangePasswordUriService(mockHttpFactory, uriService);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    public async Task GetChangePasswordUri_WhenDomainIsNullOrEmpty_ReturnsNull(string domain)
    {
        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        var uriService = new FakeUriService(_publicIp);
        var service = new ChangePasswordUriService(mockHttpFactory, uriService);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetChangePasswordUri_WhenDomainResolvesToInternalIp_ReturnsNull()
    {
        // UriService returns an IconUri with a loopback IP, which makes IsValid return false
        var uriService = new FakeUriService(_loopbackIp);

        var mockedHandler = new MockedHttpMessageHandler();
        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(mockedHandler.ToHttpClient());

        var service = new ChangePasswordUriService(mockHttpFactory, uriService);

        var result = await service.GetChangePasswordUri("https://evil.com");

        // No HTTP requests should have been made
        Assert.Empty(mockedHandler.CapturedRequests);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetChangePasswordUri_WhenDnsResolutionFails_ReturnsNull()
    {
        var uriService = new FakeUriService(_publicIp, shouldSucceed: false);

        var mockedHandler = new MockedHttpMessageHandler();
        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(mockedHandler.ToHttpClient());

        var service = new ChangePasswordUriService(mockHttpFactory, uriService);

        var result = await service.GetChangePasswordUri("https://nonexistent.invalid");

        Assert.Empty(mockedHandler.CapturedRequests);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetChangePasswordUri_WhenRedirectTargetsInternalIp_ReturnsNull()
    {
        // Initial URI resolves to a public IP, but redirects resolve to a loopback IP
        var uriService = new FakeUriServiceWithInternalRedirect(_publicIp, _loopbackIp);

        var mockedHandler = new MockedHttpMessageHandler();

        // Both endpoints redirect (simulating attacker redirect to localhost)
        mockedHandler
            .When(r => r.RequestUri!.AbsolutePath.Contains("resource-that-should-not-exist"))
            .RespondWith(HttpStatusCode.Redirect)
            .WithHeader("Location", "http://localhost:5000/some-path")
            .WithContent(new StringContent(""));

        mockedHandler
            .When(r => r.RequestUri!.AbsolutePath == "/.well-known/change-password")
            .RespondWith(HttpStatusCode.Redirect)
            .WithHeader("Location", "http://localhost:5000/version")
            .WithContent(new StringContent(""));

        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(mockedHandler.ToHttpClient());

        var service = new ChangePasswordUriService(mockHttpFactory, uriService);

        var result = await service.GetChangePasswordUri("https://attacker.com");

        Assert.Null(result);
    }
}
