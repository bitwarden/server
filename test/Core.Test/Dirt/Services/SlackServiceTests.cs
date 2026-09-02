#nullable enable

using System.Net;
using System.Text.Json;
using System.Web;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class SlackServiceTests
{
    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private const string _token = "xoxb-test-token";

    public SlackServiceTests()
    {
        _handler = new MockedHttpMessageHandler();
        _httpClient = _handler.ToHttpClient();
    }

    private SutProvider<SlackService> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(SlackService.HttpClientName).Returns(_httpClient);

        var globalSettings = Substitute.For<GlobalSettings>();
        globalSettings.Slack.ApiBaseUrl.Returns("https://slack.com/api");

        return new SutProvider<SlackService>()
            .SetDependency(clientFactory)
            .SetDependency(globalSettings)
            .Create();
    }

    [Fact]
    public async Task GetChannelIdsAsync_ReturnsCorrectChannelIds()
    {
        var response = JsonSerializer.Serialize(
            new
            {
                ok = true,
                channels =
                    new[] {
                        new { id = "C12345", name = "general" },
                        new { id = "C67890", name = "random" }
                    },
                response_metadata = new { next_cursor = "" }
            }
        );
        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(response));

        var sutProvider = GetSutProvider();
        var channelNames = new List<string> { "general", "random" };
        var result = await sutProvider.Sut.GetChannelIdsAsync(_token, channelNames);

        Assert.Equal(2, result.Count);
        Assert.Contains("C12345", result);
        Assert.Contains("C67890", result);
    }

    [Fact]
    public async Task GetChannelIdsAsync_WithPagination_ReturnsCorrectChannelIds()
    {
        var firstPageResponse = JsonSerializer.Serialize(
            new
            {
                ok = true,
                channels = new[] { new { id = "C12345", name = "general" } },
                response_metadata = new { next_cursor = "next_cursor_value" }
            }
        );
        var secondPageResponse = JsonSerializer.Serialize(
            new
            {
                ok = true,
                channels = new[] { new { id = "C67890", name = "random" } },
                response_metadata = new { next_cursor = "" }
            }
        );

        _handler.When("https://slack.com/api/conversations.list?types=public_channel%2cprivate_channel&limit=1000")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(firstPageResponse));
        _handler.When("https://slack.com/api/conversations.list?types=public_channel%2cprivate_channel&limit=1000&cursor=next_cursor_value")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(secondPageResponse));

        var sutProvider = GetSutProvider();
        var channelNames = new List<string> { "general", "random" };

        var result = await sutProvider.Sut.GetChannelIdsAsync(_token, channelNames);

        Assert.Equal(2, result.Count);
        Assert.Contains("C12345", result);
        Assert.Contains("C67890", result);
    }

    [Fact]
    public async Task GetChannelIdsAsync_ApiError_ReturnsEmptyResult()
    {
        var errorResponse = JsonSerializer.Serialize(
            new { ok = false, error = "rate_limited" }
        );

        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.TooManyRequests)
            .WithContent(new StringContent(errorResponse));

        var sutProvider = GetSutProvider();
        var channelNames = new List<string> { "general", "random" };

        var result = await sutProvider.Sut.GetChannelIdsAsync(_token, channelNames);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetChannelIdsAsync_NoChannelsFound_ReturnsEmptyResult()
    {
        var emptyResponse = JsonSerializer.Serialize(
            new
            {
                ok = true,
                channels = Array.Empty<string>(),
                response_metadata = new { next_cursor = "" }
            });

        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(emptyResponse));

        var sutProvider = GetSutProvider();
        var channelNames = new List<string> { "general", "random" };
        var result = await sutProvider.Sut.GetChannelIdsAsync(_token, channelNames);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetChannelIdAsync_NoChannelFound_ReturnsEmptyResult()
    {
        var emptyResponse = JsonSerializer.Serialize(
            new
            {
                ok = true,
                channels = Array.Empty<string>(),
                response_metadata = new { next_cursor = "" }
            });

        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(emptyResponse));

        var sutProvider = GetSutProvider();
        var result = await sutProvider.Sut.GetChannelIdAsync(_token, "general");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetChannelIdAsync_ReturnsCorrectChannelId()
    {
        var sutProvider = GetSutProvider();
        var response = new
        {
            ok = true,
            channels = new[]
            {
                new { id = "C12345", name = "general" },
                new { id = "C67890", name = "random" }
            },
            response_metadata = new { next_cursor = "" }
        };

        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(JsonSerializer.Serialize(response)));

        var result = await sutProvider.Sut.GetChannelIdAsync(_token, "general");

        Assert.Equal("C12345", result);
    }

    [Fact]
    public async Task GetDmChannelByEmailAsync_ReturnsCorrectDmChannelId()
    {
        var sutProvider = GetSutProvider();
        var email = "user@example.com";
        var userId = "U12345";
        var dmChannelId = "D67890";

        var userResponse = new
        {
            ok = true,
            user = new { id = userId }
        };

        var dmResponse = new
        {
            ok = true,
            channel = new { id = dmChannelId }
        };

        _handler.When($"https://slack.com/api/users.lookupByEmail?email={email}")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(JsonSerializer.Serialize(userResponse)));

        _handler.When("https://slack.com/api/conversations.open")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(JsonSerializer.Serialize(dmResponse)));

        var result = await sutProvider.Sut.GetDmChannelByEmailAsync(_token, email);

        Assert.Equal(dmChannelId, result);
    }

    [Fact]
    public async Task GetDmChannelByEmailAsync_ApiErrorDmResponse_ReturnsEmptyString()
    {
        var sutProvider = GetSutProvider();
        var email = "user@example.com";
        var userId = "U12345";

        var userResponse = new
        {
            ok = true,
            user = new { id = userId }
        };

        var dmResponse = new
        {
            ok = false,
            error = "An error occured"
        };

        _handler.When($"https://slack.com/api/users.lookupByEmail?email={email}")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(JsonSerializer.Serialize(userResponse)));

        _handler.When("https://slack.com/api/conversations.open")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(JsonSerializer.Serialize(dmResponse)));

        var result = await sutProvider.Sut.GetDmChannelByEmailAsync(_token, email);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetDmChannelByEmailAsync_ApiErrorUnparsableDmResponse_ReturnsEmptyString()
    {
        var sutProvider = GetSutProvider();
        var email = "user@example.com";
        var userId = "U12345";

        var userResponse = new
        {
            ok = true,
            user = new { id = userId }
        };

        _handler.When($"https://slack.com/api/users.lookupByEmail?email={email}")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(JsonSerializer.Serialize(userResponse)));

        _handler.When("https://slack.com/api/conversations.open")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("NOT JSON"));

        var result = await sutProvider.Sut.GetDmChannelByEmailAsync(_token, email);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetDmChannelByEmailAsync_ApiErrorUserResponse_ReturnsEmptyString()
    {
        var sutProvider = GetSutProvider();
        var email = "user@example.com";

        var userResponse = new
        {
            ok = false,
            error = "An error occurred"
        };

        _handler.When($"https://slack.com/api/users.lookupByEmail?email={email}")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(JsonSerializer.Serialize(userResponse)));

        var result = await sutProvider.Sut.GetDmChannelByEmailAsync(_token, email);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetDmChannelByEmailAsync_ApiErrorUnparsableUserResponse_ReturnsEmptyString()
    {
        var sutProvider = GetSutProvider();
        var email = "user@example.com";

        _handler.When($"https://slack.com/api/users.lookupByEmail?email={email}")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Not JSON"));

        var result = await sutProvider.Sut.GetDmChannelByEmailAsync(_token, email);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetRedirectUrl_ReturnsCorrectUrl()
    {
        var sutProvider = GetSutProvider();
        var clientId = sutProvider.GetDependency<GlobalSettings>().Slack.ClientId;
        var scopes = sutProvider.GetDependency<GlobalSettings>().Slack.Scopes;
        var callbackUrl = "https://example.com/callback";
        var state = Guid.NewGuid().ToString();
        var result = sutProvider.Sut.GetRedirectUrl(callbackUrl, state);

        var uri = new Uri(result);
        var query = HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal(clientId, query["client_id"]);
        Assert.Equal(scopes, query["scope"]);
        Assert.Equal(callbackUrl, query["redirect_uri"]);
        Assert.Equal(state, query["state"]);
        Assert.Equal("slack.com", uri.Host);
        Assert.Equal("/oauth/v2/authorize", uri.AbsolutePath);
    }

    [Fact]
    public async Task ObtainTokenViaOAuth_ReturnsAccessToken_WhenSuccessful()
    {
        var sutProvider = GetSutProvider();
        var jsonResponse = JsonSerializer.Serialize(new
        {
            ok = true,
            access_token = "test-access-token"
        });

        _handler.When("https://slack.com/api/oauth.v2.access")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(jsonResponse));

        var result = await sutProvider.Sut.ObtainTokenViaOAuth("test-code", "https://example.com/callback");

        Assert.Equal("test-access-token", result);
    }

    [Theory]
    [InlineData("test-code", "")]
    [InlineData("", "https://example.com/callback")]
    [InlineData("", "")]
    public async Task ObtainTokenViaOAuth_ReturnsEmptyString_WhenCodeOrRedirectUrlIsEmpty(string code, string redirectUrl)
    {
        var sutProvider = GetSutProvider();
        var result = await sutProvider.Sut.ObtainTokenViaOAuth(code, redirectUrl);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ObtainTokenViaOAuth_ReturnsEmptyString_WhenErrorResponse()
    {
        var sutProvider = GetSutProvider();
        var jsonResponse = JsonSerializer.Serialize(new
        {
            ok = false,
            error = "invalid_code"
        });

        _handler.When("https://slack.com/api/oauth.v2.access")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(jsonResponse));

        var result = await sutProvider.Sut.ObtainTokenViaOAuth("test-code", "https://example.com/callback");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ObtainTokenViaOAuth_ReturnsEmptyString_WhenHttpCallFails()
    {
        var sutProvider = GetSutProvider();
        _handler.When("https://slack.com/api/oauth.v2.access")
            .RespondWith(HttpStatusCode.InternalServerError)
            .WithContent(new StringContent(string.Empty));

        var result = await sutProvider.Sut.ObtainTokenViaOAuth("test-code", "https://example.com/callback");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SendSlackMessageByChannelId_Success_ReturnsSuccessfulResponse()
    {
        var sutProvider = GetSutProvider();
        var channelId = "C12345";
        var message = "Hello, Slack!";

        var jsonResponse = JsonSerializer.Serialize(new
        {
            ok = true,
            channel = channelId,
        });

        _handler.When(HttpMethod.Post)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(jsonResponse));

        var result = await sutProvider.Sut.SendSlackMessageByChannelIdAsync(_token, message, channelId);

        // Response was parsed correctly
        Assert.NotNull(result);
        Assert.True(result.Ok);

        // Request was sent correctly
        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal($"Bearer {_token}", request.Headers.Authorization.ToString());
        Assert.NotNull(request.Content);
        var returned = (await request.Content.ReadAsStringAsync());
        var json = JsonDocument.Parse(returned);
        Assert.Equal(message, json.RootElement.GetProperty("text").GetString() ?? string.Empty);
        Assert.Equal(channelId, json.RootElement.GetProperty("channel").GetString() ?? string.Empty);
    }

    [Fact]
    public async Task SendSlackMessageByChannelId_Failure_ReturnsErrorResponse()
    {
        var sutProvider = GetSutProvider();
        var channelId = "C12345";
        var message = "Hello, Slack!";

        var jsonResponse = JsonSerializer.Serialize(new
        {
            ok = false,
            channel = channelId,
            error = "error"
        });

        _handler.When(HttpMethod.Post)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(jsonResponse));

        var result = await sutProvider.Sut.SendSlackMessageByChannelIdAsync(_token, message, channelId);

        // Response was parsed correctly
        Assert.NotNull(result);
        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task SendSlackMessageByChannelIdAsync_InvalidJson_ReturnsNull()
    {
        var sutProvider = GetSutProvider();
        var channelId = "C12345";
        var message = "Hello world!";

        _handler.When(HttpMethod.Post)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Not JSON"));

        var result = await sutProvider.Sut.SendSlackMessageByChannelIdAsync(_token, message, channelId);

        Assert.Null(result);
    }

    [Fact]
    public async Task SendSlackMessageByChannelIdAsync_HttpServerError_ReturnsNull()
    {
        var sutProvider = GetSutProvider();
        var channelId = "C12345";
        var message = "Hello world!";

        _handler.When(HttpMethod.Post)
            .RespondWith(HttpStatusCode.InternalServerError)
            .WithContent(new StringContent(string.Empty));

        var result = await sutProvider.Sut.SendSlackMessageByChannelIdAsync(_token, message, channelId);

        Assert.Null(result);
    }
}
