using System.Net;
using System.Net.Http.Json;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class WebhookEventHandlerTests
{
    private readonly MockedHttpMessageHandler _handler;
    private HttpClient _httpClient;

    private const string _webhookUrl = "http://localhost/test/event";

    public WebhookEventHandlerTests()
    {
        _handler = new MockedHttpMessageHandler();
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.OK)
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));
        _httpClient = _handler.ToHttpClient();
    }

    public SutProvider<WebhookEventHandler> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(WebhookEventHandler.HttpClientName).Returns(_httpClient);

        var globalSettings = new GlobalSettings();
        globalSettings.EventLogging.WebhookUrl = _webhookUrl;

        return new SutProvider<WebhookEventHandler>()
            .SetDependency(globalSettings)
            .SetDependency(clientFactory)
            .Create();
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_PostsEventToUrl(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider();

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual<string>(WebhookEventHandler.HttpClientName))
        );

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        var returned = await request.Content.ReadFromJsonAsync<EventMessage>();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(_webhookUrl, request.RequestUri.ToString());
        AssertHelper.AssertPropertyEqual(eventMessage, returned, new[] { "IdempotencyId" });
    }

    [Theory, BitAutoData]
    public async Task HandleEventManyAsync_PostsEventsToUrl(IEnumerable<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider();

        await sutProvider.Sut.HandleManyEventAsync(eventMessages);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual<string>(WebhookEventHandler.HttpClientName))
        );

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        var returned = request.Content.ReadFromJsonAsAsyncEnumerable<EventMessage>();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(_webhookUrl, request.RequestUri.ToString());
        AssertHelper.AssertPropertyEqual(eventMessages, returned, new[] { "IdempotencyId" });
    }
}
