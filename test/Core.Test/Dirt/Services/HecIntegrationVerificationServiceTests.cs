using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.MockedHttpClient;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class HecIntegrationVerificationServiceTests
{
    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    private const string _scheme = "Splunk";
    private const string _token = "test-token";
    private static readonly Uri _hecUri = new Uri("https://splunk.example.com:8088/services/collector");
    private static readonly HecIntegration _integration = new(_hecUri, _scheme, _token);

    public HecIntegrationVerificationServiceTests()
    {
        _handler = new MockedHttpMessageHandler();
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.OK)
            .WithContent(new StringContent(string.Empty));
        _httpClient = _handler.ToHttpClient();
    }

    private SutProvider<HecIntegrationVerificationService> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(HecIntegrationVerificationService.HttpClientName).Returns(_httpClient);

        return new SutProvider<HecIntegrationVerificationService>()
            .SetDependency(clientFactory)
            .SetDependency(Substitute.For<ILogger<HecIntegrationVerificationService>>())
            .Create();
    }

    private HecIntegrationVerificationService GetSutWithThrowingHandler<TException>(TException exception)
        where TException : Exception
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        var httpClient = new HttpClient(new ThrowingHttpMessageHandler<TException>(exception));
        clientFactory.CreateClient(HecIntegrationVerificationService.HttpClientName)
            .Returns(httpClient);

        return new HecIntegrationVerificationService(
            clientFactory,
            Substitute.For<ILogger<HecIntegrationVerificationService>>());
    }

    [Theory, BitAutoData]
    public async Task VerifyAsync_200_ReturnsSuccess(Guid organizationId)
    {
        var sutProvider = GetSutProvider();

        var result = await sutProvider.Sut.VerifyAsync(_integration, organizationId);

        Assert.True(result.Success);
        Assert.Null(result.FailureReason);

        Assert.Single(_handler.CapturedRequests);
        var request = _handler.CapturedRequests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(_hecUri, request.RequestUri);
        Assert.Equal(new AuthenticationHeaderValue(_scheme, _token), request.Headers.Authorization);

        var body = await request.Content!.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(organizationId.ToString(), payload.GetProperty("event").GetProperty("organizationId").GetString());
    }

    [Theory, BitAutoData]
    public async Task VerifyAsync_401_ReturnsAuthFailure(Guid organizationId)
    {
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.Unauthorized)
            .WithContent(new StringContent(string.Empty));
        var sutProvider = GetSutProvider();

        var result = await sutProvider.Sut.VerifyAsync(_integration, organizationId);

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("Authentication failed", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task VerifyAsync_403_ReturnsAuthFailure(Guid organizationId)
    {
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.Forbidden)
            .WithContent(new StringContent(string.Empty));
        var sutProvider = GetSutProvider();

        var result = await sutProvider.Sut.VerifyAsync(_integration, organizationId);

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("Authentication failed", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task VerifyAsync_404_ReturnsConfigError(Guid organizationId)
    {
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.NotFound)
            .WithContent(new StringContent(string.Empty));
        var sutProvider = GetSutProvider();

        var result = await sutProvider.Sut.VerifyAsync(_integration, organizationId);

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("404", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task VerifyAsync_400_ReturnsConfigError(Guid organizationId)
    {
        // Splunk returns 400 when the target index is not in the allowed-indexes policy
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.BadRequest)
            .WithContent(new StringContent(string.Empty));
        var sutProvider = GetSutProvider();

        var result = await sutProvider.Sut.VerifyAsync(_integration, organizationId);

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("400", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task VerifyAsync_HttpRequestException_ReturnsUnreachable(Guid organizationId)
    {
        var sut = GetSutWithThrowingHandler(new HttpRequestException("connection refused"));

        var result = await sut.VerifyAsync(_integration, organizationId);

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("unreachable", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task VerifyAsync_SsrfProtectionException_ReturnsUnreachable(Guid organizationId)
    {
        var sut = GetSutWithThrowingHandler(new SsrfProtectionException("Internal IP address detected"));

        var result = await sut.VerifyAsync(_integration, organizationId);

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("unreachable", result.FailureReason);
    }

    [Theory, BitAutoData]
    public async Task VerifyAsync_TaskCanceledException_ReturnsTimeout(Guid organizationId)
    {
        var sut = GetSutWithThrowingHandler(
            new TaskCanceledException("Request timed out", new TimeoutException()));

        var result = await sut.VerifyAsync(_integration, organizationId);

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("timed out", result.FailureReason);
    }

    private sealed class ThrowingHttpMessageHandler<TException>(TException exception) : HttpMessageHandler
        where TException : Exception
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw exception;
    }
}
