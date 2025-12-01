using System.Net;
using System.Reflection;
using Bit.Api.Dirt.Controllers;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Api.Test.Dirt;

[ControllerCustomize(typeof(HibpController))]
[SutProviderCustomize]
public class HibpControllerTests : IDisposable
{
    private readonly HttpClient _originalHttpClient;
    private readonly FieldInfo _httpClientField;

    public HibpControllerTests()
    {
        // Store original HttpClient for restoration
        _httpClientField = typeof(HibpController).GetField("_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
        _originalHttpClient = (HttpClient)_httpClientField?.GetValue(null);
    }

    public void Dispose()
    {
        // Restore original HttpClient after tests
        _httpClientField?.SetValue(null, _originalHttpClient);
    }

    [Theory, BitAutoData]
    public async Task Get_WithMissingApiKey_ThrowsBadRequestException(
        SutProvider<HibpController> sutProvider,
        string username)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.Get(username));
        Assert.Equal("HaveIBeenPwned API key not set.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task Get_WithValidApiKeyAndNoBreaches_Returns200WithEmptyArray(
        SutProvider<HibpController> sutProvider,
        string username,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = "test-api-key";
        var user = new User { Id = userId };
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(userId);

        // Mock HttpClient to return 404 (no breaches found)
        var mockHttpClient = CreateMockHttpClient(HttpStatusCode.NotFound, "");
        _httpClientField.SetValue(null, mockHttpClient);

        // Act
        var result = await sutProvider.Sut.Get(username);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("[]", contentResult.Content);
        Assert.Equal("application/json", contentResult.ContentType);
    }

    [Theory, BitAutoData]
    public async Task Get_WithValidApiKeyAndBreachesFound_Returns200WithBreachData(
        SutProvider<HibpController> sutProvider,
        string username,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = "test-api-key";
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(userId);

        var breachData = "[{\"Name\":\"Adobe\",\"Title\":\"Adobe\",\"Domain\":\"adobe.com\"}]";
        var mockHttpClient = CreateMockHttpClient(HttpStatusCode.OK, breachData);
        _httpClientField.SetValue(null, mockHttpClient);

        // Act
        var result = await sutProvider.Sut.Get(username);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(breachData, contentResult.Content);
        Assert.Equal("application/json", contentResult.ContentType);
    }

    [Theory, BitAutoData]
    public async Task Get_WithRateLimiting_RetriesWithDelay(
        SutProvider<HibpController> sutProvider,
        string username,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = "test-api-key";
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(userId);

        // First response is rate limited, second is success
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.Add("retry-after", "1");
                return Task.FromResult(response);
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("")
                });
            }
        });

        var mockHttpClient = new HttpClient(mockHandler);
        _httpClientField.SetValue(null, mockHttpClient);

        // Act
        var result = await sutProvider.Sut.Get(username);

        // Assert
        Assert.Equal(2, requestCount); // Verify retry happened
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("[]", contentResult.Content);
    }

    [Theory, BitAutoData]
    public async Task Get_WithServerError_ThrowsBadRequestException(
        SutProvider<HibpController> sutProvider,
        string username,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = "test-api-key";
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(userId);

        var mockHttpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "");
        _httpClientField.SetValue(null, mockHttpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.Get(username));
        Assert.Contains("Request failed. Status code:", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task Get_WithBadRequest_ThrowsBadRequestException(
        SutProvider<HibpController> sutProvider,
        string username,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = "test-api-key";
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(userId);

        var mockHttpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, "");
        _httpClientField.SetValue(null, mockHttpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.Get(username));
        Assert.Contains("Request failed. Status code:", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task Get_EncodesUsernameCorrectly(
        SutProvider<HibpController> sutProvider,
        Guid userId)
    {
        // Arrange
        var usernameWithSpecialChars = "test+user@example.com";
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = "test-api-key";
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(userId);

        string capturedUrl = null;
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            capturedUrl = request.RequestUri.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("")
            });
        });

        var mockHttpClient = new HttpClient(mockHandler);
        _httpClientField.SetValue(null, mockHttpClient);

        // Act
        await sutProvider.Sut.Get(usernameWithSpecialChars);

        // Assert
        Assert.NotNull(capturedUrl);
        // Username should be URL encoded (+ becomes %2B, @ becomes %40)
        Assert.Contains("test%2Buser%40example.com", capturedUrl);
    }

    [Theory, BitAutoData]
    public async Task SendAsync_IncludesRequiredHeaders(
        SutProvider<HibpController> sutProvider,
        string username,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = "test-api-key";
        sutProvider.GetDependency<GlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(userId);

        HttpRequestMessage capturedRequest = null;
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("")
            });
        });

        var mockHttpClient = new HttpClient(mockHandler);
        _httpClientField.SetValue(null, mockHttpClient);

        // Act
        await sutProvider.Sut.Get(username);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("hibp-api-key"));
        Assert.True(capturedRequest.Headers.Contains("hibp-client-id"));
        Assert.True(capturedRequest.Headers.Contains("User-Agent"));
        Assert.Equal("Bitwarden", capturedRequest.Headers.GetValues("User-Agent").First());
    }

    [Theory, BitAutoData]
    public async Task SendAsync_SelfHosted_UsesCorrectUserAgent(
        SutProvider<HibpController> sutProvider,
        string username,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().HibpApiKey = "test-api-key";
        sutProvider.GetDependency<GlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(userId);

        HttpRequestMessage capturedRequest = null;
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("")
            });
        });

        var mockHttpClient = new HttpClient(mockHandler);
        _httpClientField.SetValue(null, mockHttpClient);

        // Act
        await sutProvider.Sut.Get(username);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("User-Agent"));
        Assert.Equal("Bitwarden Self-Hosted", capturedRequest.Headers.GetValues("User-Agent").First());
    }

    /// <summary>
    /// Helper to create a mock HttpClient that returns a specific status code and content
    /// </summary>
    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        });

        return new HttpClient(mockHandler);
    }
}

/// <summary>
/// Mock HttpMessageHandler for testing HttpClient behavior
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        _sendAsync = sendAsync;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _sendAsync(request, cancellationToken);
    }
}

