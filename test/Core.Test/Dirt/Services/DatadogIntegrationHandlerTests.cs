#nullable enable

using System.Net;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Bit.Test.Common.MockedHttpClient;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class DatadogIntegrationHandlerTests
{
    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private const string _apiKey = "AUTH_TOKEN";
    private static readonly Uri _datadogUri = new Uri("https://localhost");

    public DatadogIntegrationHandlerTests()
    {
        _handler = new MockedHttpMessageHandler();
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.OK)
            .WithContent(new StringContent("<html><head><title>test</title></head><body>test</body></html>"));
        _httpClient = _handler.ToHttpClient();
    }

    private SutProvider<DatadogIntegrationHandler> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(DatadogIntegrationHandler.HttpClientName).Returns(_httpClient);

        return new SutProvider<DatadogIntegrationHandler>()
            .SetDependency(clientFactory)
            .WithFakeTimeProvider()
            .Create();
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_SuccessfulRequest_ReturnsSuccess(IntegrationMessage<DatadogIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new DatadogIntegrationConfigurationDetails(ApiKey: _apiKey, Uri: _datadogUri);

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.True(result.Success);
        Assert.Equal(result.Message, message);
        Assert.Null(result.FailureReason);

        sutProvider.GetDependency<IHttpClientFactory>().Received(1).CreateClient(
            Arg.Is(AssertHelper.AssertPropertyEqual(DatadogIntegrationHandler.HttpClientName))
        );

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.NotNull(request);
        Assert.NotNull(request.Content);
        var returned = await request.Content.ReadAsStringAsync();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(_apiKey, request.Headers.GetValues("DD-API-KEY").Single());
        Assert.Equal(_datadogUri, request.RequestUri);
        AssertHelper.AssertPropertyEqual(message.RenderedTemplate, returned);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_TooManyRequests_ReturnsFailureSetsDelayUntilDate(IntegrationMessage<DatadogIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        var now = new DateTime(2014, 3, 2, 1, 0, 0, DateTimeKind.Utc);
        var retryAfter = now.AddSeconds(60);

        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(now);
        message.Configuration = new DatadogIntegrationConfigurationDetails(ApiKey: _apiKey, Uri: _datadogUri);

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
    public async Task HandleAsync_TooManyRequestsWithDate_ReturnsFailureSetsDelayUntilDate(IntegrationMessage<DatadogIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        var now = new DateTime(2014, 3, 2, 1, 0, 0, DateTimeKind.Utc);
        var retryAfter = now.AddSeconds(60);
        message.Configuration = new DatadogIntegrationConfigurationDetails(ApiKey: _apiKey, Uri: _datadogUri);

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
    public async Task HandleAsync_InternalServerError_ReturnsFailureSetsRetryable(IntegrationMessage<DatadogIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new DatadogIntegrationConfigurationDetails(ApiKey: _apiKey, Uri: _datadogUri);

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
    public async Task HandleAsync_UnexpectedRedirect_ReturnsFailureNotRetryable(IntegrationMessage<DatadogIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new DatadogIntegrationConfigurationDetails(ApiKey: _apiKey, Uri: _datadogUri);

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
