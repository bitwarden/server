using System.Net;
using System.Net.Http.Json;
using Bit.Core.Platform.Mail.Delivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Bit.MailDelivery.Test.SmtpOAuth;

public class OAuthHandlerTests
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly ILogger _logger;
    private readonly TestOAuthHandler _sut;

    public OAuthHandlerTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("SmtpOAuth").Returns(_mockHandler.ToHttpClient());

        _fakeTimeProvider = new FakeTimeProvider();
        _logger = Substitute.For<ILogger>();

        _sut = new TestOAuthHandler(httpClientFactory, _fakeTimeProvider, _logger);
    }

    private void RespondWithToken(string accessToken = "test_token", int expiresIn = 3599)
    {
        _mockHandler.When(HttpMethod.Post, "https://example.com/token").Respond(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = accessToken, expires_in = expiresIn }),
            });
    }

    [Fact]
    public async Task ReturnsCredential_FromSuccessfulTokenResponse()
    {
        RespondWithToken();

        var credentials = await _sut.GetAsync(CancellationToken.None);

        Assert.Equal("XOAUTH2", credentials.MechanismName);
    }

    [Fact]
    public async Task ReturnsCachedCredential_UntilSkewWindow()
    {
        RespondWithToken();
        await _sut.GetAsync(CancellationToken.None);
        await _sut.GetAsync(CancellationToken.None);
        Assert.Equal(1, _sut.BuildContentCallCount);

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(3600));
        await _sut.GetAsync(CancellationToken.None);
        Assert.Equal(2, _sut.BuildContentCallCount);
    }

    [Fact]
    public async Task ParallelCalls_OnlyCallsEndpointOnce()
    {
        _mockHandler.When(HttpMethod.Post, "https://example.com/token").Respond(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "test_token", expires_in = 3599 }),
            };
        });

        await Parallel.ForAsync(0, 3, async (_, _) => await _sut.GetAsync(CancellationToken.None));

        Assert.Equal(1, _sut.BuildContentCallCount);
    }

    [Fact]
    public async Task MissingExpiresIn_DoesNotCacheToken()
    {
        _mockHandler.When(HttpMethod.Post, "https://example.com/token").Respond(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "test_token" }),
            });

        await _sut.GetAsync(CancellationToken.None);
        await _sut.GetAsync(CancellationToken.None);

        Assert.Equal(2, _sut.BuildContentCallCount);
    }

    [Fact]
    public async Task InvalidateIfCurrent_ClearsCache_WhenTokenStillMatches()
    {
        RespondWithToken(accessToken: "token_a");
        await _sut.GetAsync(CancellationToken.None);

        await _sut.InvalidateIfCurrentAsync("token_a", CancellationToken.None);

        RespondWithToken(accessToken: "token_b");
        await _sut.GetAsync(CancellationToken.None);
        Assert.Equal(2, _sut.BuildContentCallCount);
    }

    [Fact]
    public async Task InvalidateIfCurrent_DoesNotClearCache_WhenTokenAlreadyReplaced()
    {
        RespondWithToken(accessToken: "token_a");
        await _sut.GetAsync(CancellationToken.None);

        // A concurrent refresh already replaced token_a with token_b before this stale caller
        // tries to invalidate the token it was rejected for.
        await _sut.InvalidateIfCurrentAsync("stale_token_never_cached", CancellationToken.None);

        await _sut.GetAsync(CancellationToken.None);
        Assert.Equal(1, _sut.BuildContentCallCount);
    }

    [Fact]
    public async Task BadJsonResponse_ThrowsTokenException_WithoutLeakingJsonException()
    {
        _mockHandler.When(HttpMethod.Post, "https://example.com/token").Respond(
            HttpStatusCode.InternalServerError, "text/plain", "upstream proxy error");

        var exception = await Assert.ThrowsAsync<TokenException>(() => _sut.GetAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Contains("not a recognizable error payload", exception.Message);
    }

    [Fact]
    public async Task ErrorResponseWithoutDescription_ThrowsTokenException_WithErrorCode()
    {
        _mockHandler.When(HttpMethod.Post, "https://example.com/token").Respond(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new { error = "invalid_client" }),
            });

        var exception = await Assert.ThrowsAsync<TokenException>(() => _sut.GetAsync(CancellationToken.None));

        Assert.Contains("invalid_client", exception.Message);
        Assert.DoesNotContain("not a recognizable error payload", exception.Message);
        Assert.DoesNotContain("with description", exception.Message);
    }

    [Fact]
    public async Task WellFormedErrorResponse_ThrowsTokenException_WithErrorDetails()
    {
        _mockHandler.When(HttpMethod.Post, "https://example.com/token").Respond(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new { error = "invalid_scope", error_description = "bad scope" }),
            });

        var exception = await Assert.ThrowsAsync<TokenException>(() => _sut.GetAsync(CancellationToken.None));

        Assert.Contains("invalid_scope", exception.Message);
        Assert.Contains("bad scope", exception.Message);
        Assert.Equal("example.com", exception.TokenEndpointHost);
        Assert.DoesNotContain("https://example.com/token", exception.Message);
    }

    [Fact]
    public void TokenSuccessResponse_ToString_RedactsAccessToken()
    {
        var response = new TokenSuccessResponse { AccessToken = "secret-token-value", ExpiresIn = 3600 };

        var result = response.ToString();

        Assert.DoesNotContain("secret-token-value", result);
        Assert.Contains("[redacted]", result);
        Assert.Contains("3600", result);
    }
}

internal class TestOAuthHandler(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger logger)
    : OAuthHandler(httpClientFactory, timeProvider, logger, "https://example.com/token", "test_username")
{
    public int BuildContentCallCount { get; private set; }

    protected override Dictionary<string, string> BuildContent()
    {
        BuildContentCallCount++;
        return [];
    }
}
