using System.Net;
using System.Text.Json;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

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

        return new SutProvider<SlackService>()
            .SetDependency(clientFactory)
            .Create();
    }

    [Fact]
    public async Task GetChannelIdsAsync_Returns_Correct_ChannelIds()
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
    public async Task GetChannelIdsAsync_Handles_Pagination_Correctly()
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
    public async Task GetChannelIdsAsync_Handles_Api_Error_Gracefully()
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
    public async Task GetChannelIdsAsync_Returns_Empty_When_No_Channels_Found()
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
    public async Task GetChannelIdAsync_Returns_Correct_ChannelId()
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
    public async Task GetDmChannelByEmailAsync_Returns_Correct_DmChannelId()
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
    public async Task SendSlackMessageByChannelId_Sends_Correct_Message()
    {
        var sutProvider = GetSutProvider();
        var channelId = "C12345";
        var message = "Hello, Slack!";

        _handler.When(HttpMethod.Post)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(string.Empty));

        await sutProvider.Sut.SendSlackMessageByChannelId(_token, message, channelId);

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
}
