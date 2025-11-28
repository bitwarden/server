using System.Net;
using System.Net.Http.Headers;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Bit.Test.Common.MockedHttpClient;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class WebhookIntegrationHandlerTests
{
    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private const string _scheme = "Bearer";
    private const string _token = "AUTH_TOKEN";
    private static readonly Uri _webhookUri = new Uri("https://localhost");

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
            .WithFakeTimeProvider()
            .Create();
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_SuccessfulRequestWithoutAuth_ReturnsSuccess(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUri);

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.True(result.Success);
        Assert.Equal(result.Message, message);
        Assert.Empty(result.FailureReason);

        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(WebhookIntegrationHandler.HttpClientName))
        );

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        Assert.NotNull(request.Content);
        var returned = await request.Content.ReadAsStringAsync();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Null(request.Headers.Authorization);
        Assert.Equal(_webhookUri, request.RequestUri);
        AssertHelper.AssertPropertyEqual(message.RenderedTemplate, returned);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_SuccessfulRequestWithAuthorizationHeader_ReturnsSuccess(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUri, _scheme, _token);

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.True(result.Success);
        Assert.Equal(result.Message, message);
        Assert.Empty(result.FailureReason);

        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(WebhookIntegrationHandler.HttpClientName))
        );

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        Assert.NotNull(request.Content);
        var returned = await request.Content.ReadAsStringAsync();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new AuthenticationHeaderValue(_scheme, _token), request.Headers.Authorization);
        Assert.Equal(_webhookUri, request.RequestUri);
        AssertHelper.AssertPropertyEqual(message.RenderedTemplate, returned);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_TooManyRequests_ReturnsFailureSetsDelayUntilDate(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        var now = new DateTime(2014, 3, 2, 1, 0, 0, DateTimeKind.Utc);
        var retryAfter = now.AddSeconds(60);

        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(now);
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUri, _scheme, _token);

        _handler.Fallback
            .WithStatusCode(HttpStatusCode.TooManyRequests)
            .WithHeader("Retry-After", "60")
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
        Assert.Equal(result.Message, message);
        Assert.True(result.DelayUntilDate.HasValue);
        Assert.Equal(retryAfter, result.DelayUntilDate.Value);
        Assert.Equal("Too Many Requests", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_TooManyRequestsWithDate_ReturnsFailureSetsDelayUntilDate(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        var now = new DateTime(2014, 3, 2, 1, 0, 0, DateTimeKind.Utc);
        var retryAfter = now.AddSeconds(60);
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUri, _scheme, _token);

        _handler.Fallback
            .WithStatusCode(HttpStatusCode.TooManyRequests)
            .WithHeader("Retry-After", retryAfter.ToString("r"))
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
        Assert.Equal(result.Message, message);
        Assert.True(result.DelayUntilDate.HasValue);
        Assert.Equal(retryAfter, result.DelayUntilDate.Value);
        Assert.Equal("Too Many Requests", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_InternalServerError_ReturnsFailureSetsRetryable(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUri, _scheme, _token);

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
        message.Configuration = new WebhookIntegrationConfigurationDetails(_webhookUri, _scheme, _token);

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
