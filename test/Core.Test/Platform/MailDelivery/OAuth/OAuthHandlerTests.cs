using System.Net;
using System.Net.Http.Json;
using Bit.Core.Platform.MailDelivery;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RichardSzalay.MockHttp;
using Xunit;

namespace Bit.Core.Test.Platform.MailDelivery;

public class OAuthHandlerTests
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly FakeTimeProvider _fakeTimeProvider;

    private readonly TestOAuthHandler _sut;

    public OAuthHandlerTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("SmtpOAuth")
            .Returns(_mockHandler.ToHttpClient());

        _fakeTimeProvider = new FakeTimeProvider();

        _sut = new TestOAuthHandler(httpClientFactory, _fakeTimeProvider);
    }

    [Fact]
    public async Task ReturnsCredential_FromSuccessfulTokenResponse()
    {
        _mockHandler
            .When(HttpMethod.Post, "https://example.com/token")
            .WithFormData([])
            .Respond((_) =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "test_token",
                        expires_in = 3599,
                    }),
                };
            });

        var credentials = await _sut.GetAsync(CancellationToken.None);

        Assert.NotNull(credentials);
        Assert.Equal("XOAUTH2", credentials.MechanismName);
        Assert.Equal("test_username", credentials.Credentials.UserName);
        Assert.Equal("test_token", credentials.Credentials.Password);
    }

    [Fact]
    public async Task ReturnsCachedCredential_FromSecondGetAsyncCall()
    {
        _mockHandler
            .When(HttpMethod.Post, "https://example.com/token")
            .WithFormData([])
            .Respond((_) =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "test_token",
                        expires_in = 3599,
                    }),
                };
            });

        var credentials = await _sut.GetAsync(CancellationToken.None);
        var credentialsCached = await _sut.GetAsync(CancellationToken.None);

        Assert.NotNull(credentials);
        Assert.Equal("XOAUTH2", credentials.MechanismName);
        Assert.Equal("test_username", credentials.Credentials.UserName);
        Assert.Equal("test_token", credentials.Credentials.Password);

        Assert.NotNull(credentialsCached);
        Assert.Equal("XOAUTH2", credentialsCached.MechanismName);
        Assert.Equal("test_username", credentialsCached.Credentials.UserName);
        Assert.Equal("test_token", credentialsCached.Credentials.Password);

        Assert.Equal(1, _sut.BuildContentCallCount);
    }

    [Theory]
    [InlineData(3540)]
    [InlineData(3599)]
    [InlineData(3700)]
    public async Task ReturnsCachedCredential_UntilTokenIsAboutToExpire(int expireSeconds)
    {
        _mockHandler
            .When(HttpMethod.Post, "https://example.com/token")
            .WithFormData([])
            .Respond((_) =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "test_token",
                        expires_in = 3599,
                    }),
                };
            });

        var credentials = await _sut.GetAsync(CancellationToken.None);

        Assert.NotNull(credentials);

        var credentialsCached = await _sut.GetAsync(CancellationToken.None);

        Assert.NotNull(credentialsCached);

        Assert.Equal(1, _sut.BuildContentCallCount);

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(expireSeconds));

        var newCredentials = await _sut.GetAsync(CancellationToken.None);
        Assert.NotNull(newCredentials);

        Assert.Equal(2, _sut.BuildContentCallCount);
    }

    [Fact]
    public async Task ParallelCalls_OnlyCallsEndpointOnce()
    {
        _mockHandler
            .When(HttpMethod.Post, "https://example.com/token")
            .WithFormData([])
            .Respond(async () =>
            {
                // Pretend the call takes a little time
                await Task.Delay(TimeSpan.FromMilliseconds(10));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "test_token",
                        expires_in = 3599,
                    }),
                };
            });

        // Call endpoint three times in Parallel
        await Parallel.ForAsync(0, 3, async (_, _) =>
        {
            var credentials = await _sut.GetAsync(CancellationToken.None);

            Assert.Equal("test_token", credentials.Credentials.Password);
        });

        Assert.Equal(1, _sut.BuildContentCallCount);
    }

    public static IEnumerable<object[]> BadResponseTestData()
    {
        static object[] Data(object data, HttpStatusCode httpStatusCode, string errorMessage)
        {
            return [JsonContent.Create(data), httpStatusCode, errorMessage];
        }

        // Microsoft Errors: https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow#error-response-1
        yield return Data(
            new
            {
                error = "invalid_scope",
                error_description = "AADSTS70011: The provided value for the input parameter 'scope' is not valid. The scope https://foo.microsoft.com/.default is not valid.\r\nTrace ID: 0000aaaa-11bb-cccc-dd22-eeeeee333333\r\nCorrelation ID: aaaa0000-bb11-2222-33cc-444444dddddd\r\nTimestamp: 2016-01-09 02:02:12Z",
                error_codes = new int[] { 70011 },
                timestamp = DateTime.UtcNow,
                trace_id = Guid.NewGuid(),
                correlation_id = Guid.NewGuid(),
            },
            HttpStatusCode.BadRequest,
            "Error during request to https://example.com/token (BadRequest). Encountered error invalid_scope with description 'AADSTS70011: The provided value for the input parameter 'scope' is not valid. The scope https://foo.microsoft.com/.default is not valid.\r\nTrace ID: 0000aaaa-11bb-cccc-dd22-eeeeee333333\r\nCorrelation ID: aaaa0000-bb11-2222-33cc-444444dddddd\r\nTimestamp: 2016-01-09 02:02:12Z'"
        );
    }

    [Theory]
    [MemberData(nameof(BadResponseTestData))]
    public async Task BadResponse_ThrowsError(JsonContent responseContent, HttpStatusCode httpStatusCode, string errorMessage)
    {
        _mockHandler
            .When(HttpMethod.Post, "https://example.com/token")
            .WithFormData([])
            .Respond((_) =>
            {
                return new HttpResponseMessage(httpStatusCode)
                {
                    Content = responseContent,
                };
            });

        var tokenException = await Assert.ThrowsAsync<TokenException>(
            async () => await _sut.GetAsync(CancellationToken.None)
        );

        Assert.Equal(errorMessage, tokenException.Message);
    }
}

internal class TestOAuthHandler(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
    : OAuthHandler(httpClientFactory, timeProvider, "https://example.com/token", "test_username")
{
    public int BuildContentCallCount { get; private set; }

    protected override Dictionary<string, string> BuildContent()
    {
        BuildContentCallCount++;
        return [];
    }
}
