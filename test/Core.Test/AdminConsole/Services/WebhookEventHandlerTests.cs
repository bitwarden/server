﻿using System.Net;
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

    private const string _template =
        """
        {
            "Date": "#Date#",
            "Type": "#Type#",
            "UserId": "#UserId#"
        }
        """;
    private const string _webhookUrl = "http://localhost/test/event";
    private const string _webhookUrl2 = "http://localhost/another/event";

    public WebhookEventHandlerTests()
    {
        _handler = new MockedHttpMessageHandler();
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.OK)
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));
        _httpClient = _handler.ToHttpClient();
    }

    private SutProvider<WebhookEventHandler> GetSutProvider(
        List<IntegrationConfiguration<WebhookConfiguration>> configurations)
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(WebhookEventHandler.HttpClientName).Returns(_httpClient);

        var repository = Substitute.For<IOrganizationIntegrationConfigurationRepository>();
        repository.GetConfigurationsAsync<WebhookConfiguration>(
            IntegrationType.Webhook,
            Arg.Any<Guid>(),
            Arg.Any<EventType>()
        ).Returns(configurations);

        return new SutProvider<WebhookEventHandler>()
            .SetDependency(repository)
            .SetDependency(clientFactory)
            .Create();
    }

    List<IntegrationConfiguration<WebhookConfiguration>> NoConfigurations()
    {
        return new List<IntegrationConfiguration<WebhookConfiguration>>();
    }

    List<IntegrationConfiguration<WebhookConfiguration>> OneConfiguration()
    {
        return new List<IntegrationConfiguration<WebhookConfiguration>>
        {
            new IntegrationConfiguration<WebhookConfiguration>
            {
                Configuration = new WebhookConfiguration { Url = _webhookUrl },
                Template = _template
            }
        };
    }

    List<IntegrationConfiguration<WebhookConfiguration>> TwoConfigurations()
    {
        return new List<IntegrationConfiguration<WebhookConfiguration>>
        {
            new IntegrationConfiguration<WebhookConfiguration>
            {
                Configuration = new WebhookConfiguration { Url = _webhookUrl },
                Template = _template
            },
            new IntegrationConfiguration<WebhookConfiguration>
            {
                Configuration = new WebhookConfiguration { Url = _webhookUrl2 },
                Template = _template
            }
        };
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_DoesNothingWhenNoConfigurations(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(WebhookEventHandler.HttpClientName))
        );

        Assert.Empty(_handler.CapturedRequests);
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_PostsEventToUrl(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration());

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
    public async Task HandleManyEventsAsync_DoesNothingWhenNoConfigurations(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(WebhookEventHandler.HttpClientName))
        );

        Assert.Empty(_handler.CapturedRequests);
    }


    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_PostsEventsToUrl(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(OneConfiguration());

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(WebhookEventHandler.HttpClientName))
        );

        Assert.Equal(eventMessages.Count, _handler.CapturedRequests.Count);
        var index = 0;
        foreach (var request in _handler.CapturedRequests)
        {
            Assert.NotNull(request);
            var returned = await request.Content.ReadFromJsonAsync<MockEvent>();
            var expected = MockEvent.From(eventMessages[index]);

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(_webhookUrl, request.RequestUri.ToString());
            AssertHelper.AssertPropertyEqual(expected, returned);
            index++;
        }
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_PostsEventsToMultipleUrls(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(TwoConfigurations());

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);
        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(WebhookEventHandler.HttpClientName))
        );

        using var capturedRequests = _handler.CapturedRequests.GetEnumerator();
        Assert.Equal(eventMessages.Count * 2, _handler.CapturedRequests.Count);

        foreach (var eventMessage in eventMessages)
        {
            var expected = MockEvent.From(eventMessage);

            Assert.True(capturedRequests.MoveNext());
            var request = capturedRequests.Current;
            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(_webhookUrl, request.RequestUri.ToString());
            var returned = await request.Content.ReadFromJsonAsync<MockEvent>();
            AssertHelper.AssertPropertyEqual(expected, returned);

            Assert.True(capturedRequests.MoveNext());
            request = capturedRequests.Current;
            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(_webhookUrl2, request.RequestUri.ToString());
            returned = await request.Content.ReadFromJsonAsync<MockEvent>();
            AssertHelper.AssertPropertyEqual(expected, returned);
        }
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
