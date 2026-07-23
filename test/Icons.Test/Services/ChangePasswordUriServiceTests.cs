using System.Net;
using Bit.Icons.Services;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;
using Xunit;

namespace Bit.Icons.Test.Services;

public class ChangePasswordUriServiceTests : ServiceTestBase<ChangePasswordUriService>
{
    [Theory]
    [InlineData("https://example.com", "https://example.com:443/.well-known/change-password")]
    public async Task GetChangePasswordUri_WhenBothChecksPass_ReturnsWellKnownUrl(string domain, string expectedUrl)
    {
        // Arrange
        var mockedHandler = new MockedHttpMessageHandler();

        var nonExistentUrl = $"{domain}/.well-known/resource-that-should-not-exist-whose-status-code-should-not-be-200";
        var changePasswordUrl = $"{domain}/.well-known/change-password";

        // Mock the response for the resource-that-should-not-exist request (returns 404)
        mockedHandler
            .When(nonExistentUrl)
            .RespondWith(HttpStatusCode.NotFound)
            .WithContent(new StringContent("Not found"));

        // Mock the response for the change-password request (returns 200)
        mockedHandler
            .When(changePasswordUrl)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Ok"));

        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(mockedHandler.ToHttpClient());

        var service = new ChangePasswordUriService(mockHttpFactory);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Equal(ChangePasswordUriResultType.Found, result.Type);
        Assert.Equal(expectedUrl, result.Uri);
    }

    [Theory]
    [InlineData("https://example.com")]
    public async Task GetChangePasswordUri_WhenResourceThatShouldNotExistReturns200_ReturnsNotSupported(string domain)
    {
        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        var mockedHandler = new MockedHttpMessageHandler();

        mockedHandler
            .When(HttpMethod.Get, $"{domain}/.well-known/resource-that-should-not-exist-whose-status-code-should-not-be-200")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Ok"));

        mockedHandler
            .When(HttpMethod.Get, $"{domain}/.well-known/change-password")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Ok"));

        var httpClient = mockedHandler.ToHttpClient();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(httpClient);

        var service = new ChangePasswordUriService(mockHttpFactory);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Equal(ChangePasswordUriResultType.NotSupported, result.Type);
        Assert.Null(result.Uri);
    }

    [Theory]
    [InlineData("https://example.com")]
    public async Task GetChangePasswordUri_WhenChangePasswordUrlNotFound_ReturnsNotSupported(string domain)
    {
        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        var mockedHandler = new MockedHttpMessageHandler();

        mockedHandler
            .When(HttpMethod.Get, $"{domain}/.well-known/resource-that-should-not-exist-whose-status-code-should-not-be-200")
            .RespondWith(HttpStatusCode.NotFound)
            .WithContent(new StringContent("Not found"));

        mockedHandler
            .When(HttpMethod.Get, $"{domain}/.well-known/change-password")
            .RespondWith(HttpStatusCode.NotFound)
            .WithContent(new StringContent("Not found"));

        var httpClient = mockedHandler.ToHttpClient();
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(httpClient);

        var service = new ChangePasswordUriService(mockHttpFactory);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Equal(ChangePasswordUriResultType.NotSupported, result.Type);
        Assert.Null(result.Uri);
    }

    [Theory]
    [InlineData("https://example.com")]
    public async Task GetChangePasswordUri_WhenProbeThrows_ReturnsLookupFailed(string domain)
    {
        // A transient failure must not be reported as a definitive "not supported" answer.
        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        mockHttpFactory.CreateClient("ChangePasswordUri").Returns(httpClient);

        var service = new ChangePasswordUriService(mockHttpFactory);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Equal(ChangePasswordUriResultType.LookupFailed, result.Type);
        Assert.Null(result.Uri);
    }

    [Theory]
    [InlineData("")]
    public async Task GetChangePasswordUri_WhenDomainIsNullOrEmpty_ReturnsNotSupported(string domain)
    {
        var mockHttpFactory = Substitute.For<IHttpClientFactory>();
        var service = new ChangePasswordUriService(mockHttpFactory);

        var result = await service.GetChangePasswordUri(domain);

        Assert.Equal(ChangePasswordUriResultType.NotSupported, result.Type);
        Assert.Null(result.Uri);
    }

    /// <summary>
    /// An inner handler that simulates a transient network failure by throwing on every request.
    /// </summary>
    private class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Simulated transient failure");
        }
    }
}
