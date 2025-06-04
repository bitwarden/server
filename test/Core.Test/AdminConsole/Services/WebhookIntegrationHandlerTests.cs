﻿using System.Net;
using Bit.Core.AdminConsole.Models.Data.Integrations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class WebhookIntegrationHandlerTests
{
    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private const string _webhookUrl = "http://localhost/test/event";

    public WebhookIntegrationHandlerTests()
    {
        _handler = new MockedHttpMessageHandler();
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.OK)
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));
        _httpClient = _handler.ToHttpClient();
    }

    private SutProvider<WebhookIntegrationHandler> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(WebhookIntegrationHandler.HttpClientName).Returns(_httpClient);

        return new SutProvider<WebhookIntegrationHandler>()
            .SetDependency(clientFactory)
            .Create();
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_SuccessfulRequest_ReturnsSuccess(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUrl);

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.True(result.Success);
        Assert.Equal(result.Message, message);

        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(WebhookIntegrationHandler.HttpClientName))
        );

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        var returned = await request.Content.ReadAsStringAsync();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(_webhookUrl, request.RequestUri.ToString());
        AssertHelper.AssertPropertyEqual(message.RenderedTemplate, returned);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_TooManyRequests_ReturnsFailureSetsNotBeforUtc(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUrl);

        _handler.Fallback
            .WithStatusCode(HttpStatusCode.TooManyRequests)
            .WithHeader("Retry-After", "60")
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
        Assert.Equal(result.Message, message);
        Assert.True(result.DelayUntilDate.HasValue);
        Assert.InRange(result.DelayUntilDate.Value, DateTime.UtcNow.AddSeconds(59), DateTime.UtcNow.AddSeconds(61));
        Assert.Equal("Too Many Requests", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_TooManyRequestsWithDate_ReturnsFailureSetsNotBeforUtc(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUrl);

        _handler.Fallback
            .WithStatusCode(HttpStatusCode.TooManyRequests)
            .WithHeader("Retry-After", DateTime.UtcNow.AddSeconds(60).ToString("r")) // "r" is the round-trip format: RFC1123
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
        Assert.Equal(result.Message, message);
        Assert.True(result.DelayUntilDate.HasValue);
        Assert.InRange(result.DelayUntilDate.Value, DateTime.UtcNow.AddSeconds(59), DateTime.UtcNow.AddSeconds(61));
        Assert.Equal("Too Many Requests", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_InternalServerError_ReturnsFailureSetsRetryable(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUrl);

        _handler.Fallback
            .WithStatusCode(HttpStatusCode.InternalServerError)
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
        Assert.Equal(result.Message, message);
        Assert.False(result.DelayUntilDate.HasValue);
        Assert.Equal("Internal Server Error", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_UnexpectedRedirect_ReturnsFailureNotRetryable(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUrl);

        _handler.Fallback
            .WithStatusCode(HttpStatusCode.TemporaryRedirect)
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.Equal(result.Message, message);
        Assert.Null(result.DelayUntilDate);
        Assert.Equal("Temporary Redirect", result.FailureReason);
    }
}
