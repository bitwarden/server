using System.Net;
using System.Net.Http.Json;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class WebhookEventHandlerTests
{
    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    private const string _webhookUrl = "http://localhost/test/event";

    public WebhookEventHandlerTests()
    {
        _handler = new MockedHttpMessageHandler();
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.OK)
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));
        _httpClient = _handler.ToHttpClient();
    }

    private SutProvider<WebhookEventHandler> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(WebhookEventHandler.HttpClientName).Returns(_httpClient);

        var repository = Substitute.For<IOrganizationIntegrationConfigurationRepository>();
        repository.GetConfigurationAsync<WebhookConfiguration>(
            Arg.Any<Guid>(),
            IntegrationType.Webhook,
            Arg.Any<EventType>()
        ).Returns(
            new IntegrationConfiguration<WebhookConfiguration>
            {
                Configuration = new WebhookConfiguration { ApiKey = "", Url = _webhookUrl },
                Template = "{ \"Date\": \"#Date#\", \"Type\": \"#Type#\", \"UserId\": \"#UserId#\" }"
            }
        );

        return new SutProvider<WebhookEventHandler>()
            .SetDependency(repository)
            .SetDependency(clientFactory)
            .Create();
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_PostsEventToUrl(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider();

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(WebhookEventHandler.HttpClientName))
        );

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        var returned = await request.Content.ReadFromJsonAsync<MockEvent>();
        var expected = MockEvent.From(eventMessage);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(_webhookUrl, request.RequestUri.ToString());
        AssertHelper.AssertPropertyEqual(expected, returned);
    }

    [Theory, BitAutoData]
    public async Task HandleEventManyAsync_PostsEventsToUrl(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider();

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual<string>(WebhookEventHandler.HttpClientName))
        );

        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        var returned = await request.Content.ReadFromJsonAsync<MockEvent>();
        var expected = MockEvent.From(eventMessages.First());

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(_webhookUrl, request.RequestUri.ToString());
        AssertHelper.AssertPropertyEqual(expected, returned);
    }
}

public class MockEvent(string date, string type, string userId)
{
    public string Date { get; set; } = date;
    public string Type { get; set; } = type;
    public string UserId { get; set; } = userId;

    public static MockEvent From(EventMessage eventMessage)
    {
        return new MockEvent(
            eventMessage.Date.ToString(),
            eventMessage.Type.ToString(),
            eventMessage.UserId.ToString()
        );
    }
}
