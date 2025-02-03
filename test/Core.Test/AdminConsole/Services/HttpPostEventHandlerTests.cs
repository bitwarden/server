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
public class HttpPostEventHandlerTests
{
    private readonly MockedHttpMessageHandler _handler;
    private HttpClient _httpClient;

    private const string _httpPostUrl = "http://localhost/test/event";

    public HttpPostEventHandlerTests()
    {
        _handler = new MockedHttpMessageHandler();
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.OK)
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));
        _httpClient = _handler.ToHttpClient();
    }

    public SutProvider<HttpPostEventHandler> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(HttpPostEventHandler.HttpClientName).Returns(_httpClient);

        var globalSettings = new GlobalSettings();
        globalSettings.EventLogging.RabbitMq.HttpPostUrl = _httpPostUrl;

        return new SutProvider<HttpPostEventHandler>()
            .SetDependency(globalSettings)
            .SetDependency(clientFactory)
            .Create();
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_PostsEventsToUrl(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider();
        var content = JsonContent.Create(eventMessage);

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual<string>(HttpPostEventHandler.HttpClientName))
        );

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        var returned = await request.Content.ReadFromJsonAsync<EventMessage>();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(_httpPostUrl, request.RequestUri.ToString());
        AssertHelper.AssertPropertyEqual(eventMessage, returned, new[] { "IdempotencyId" });
    }
}
