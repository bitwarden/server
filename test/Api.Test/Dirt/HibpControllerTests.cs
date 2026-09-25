using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bit.Api.Dirt.Controllers;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.MockedHttpClient;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Api.Test.Dirt;

[ControllerCustomize(typeof(HibpController))]
[SutProviderCustomize]
public class HibpControllerTests
{
    private const string _hashPrefix = "5BAA6";
    private const string _rangeUrl = $"https://api.pwnedpasswords.com/range/{_hashPrefix}";
    private const string _apiKey = "test-api-key";
    private const string _rangeData = "0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n00D4F6E8FA6EECAD2A3AA415EEC418D38EC:2";

    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    public HibpControllerTests()
    {
        _handler = new MockedHttpMessageHandler();

        // Every mocked response must carry content; the builder copies the content stream unconditionally.
        _handler.Fallback
            .WithStatusCode(HttpStatusCode.NotFound)
            .WithContent(new StringContent(string.Empty));

        _httpClient = _handler.ToHttpClient();
    }

    [Theory, BitAutoData]
    public async Task GetRangeAsync_WithSuccessfulResponse_ReturnsPlainTextPassthrough(
        SutProvider<HibpController> sutProvider)
    {
        RespondToRangeRequestWith(HttpStatusCode.OK, _rangeData);
        ConfigureSut(sutProvider);

        var result = await sutProvider.Sut.GetRangeAsync(_hashPrefix);

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(_rangeData, contentResult.Content);
        Assert.Equal("text/plain", contentResult.ContentType);
    }

    [Theory, BitAutoData]
    public async Task GetRangeAsync_ForwardsHashPrefixToPwnedPasswordsApi(
        SutProvider<HibpController> sutProvider)
    {
        RespondToRangeRequestWith(HttpStatusCode.OK, _rangeData);
        ConfigureSut(sutProvider);

        await sutProvider.Sut.GetRangeAsync(_hashPrefix);

        var request = Assert.Single(_handler.CapturedRequests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(_rangeUrl), request.RequestUri);
    }

    [Theory, BitAutoData]
    public async Task GetRangeAsync_RequestsResponsePadding(
        SutProvider<HibpController> sutProvider)
    {
        RespondToRangeRequestWith(HttpStatusCode.OK, _rangeData);
        ConfigureSut(sutProvider);

        await sutProvider.Sut.GetRangeAsync(_hashPrefix);

        var request = Assert.Single(_handler.CapturedRequests);
        Assert.Equal("true", Assert.Single(request.Headers.GetValues("Add-Padding")));
    }

    /// <summary>
    /// The pwned passwords range API is anonymous by design. Forwarding the API key or the
    /// per-user client id would let HaveIBeenPwned correlate password lookups back to a user.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetRangeAsync_DoesNotSendUserIdentifyingHeaders(
        SutProvider<HibpController> sutProvider)
    {
        RespondToRangeRequestWith(HttpStatusCode.OK, _rangeData);
        ConfigureSut(sutProvider);

        await sutProvider.Sut.GetRangeAsync(_hashPrefix);

        var request = Assert.Single(_handler.CapturedRequests);
        Assert.False(request.Headers.Contains("hibp-api-key"));
        Assert.False(request.Headers.Contains("hibp-client-id"));
        sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .GetProperUserId(default);
    }

    [Theory, BitAutoData]
    public async Task GetRangeAsync_WithoutHibpApiKey_StillSucceeds(
        SutProvider<HibpController> sutProvider)
    {
        RespondToRangeRequestWith(HttpStatusCode.OK, _rangeData);
        ConfigureSut(sutProvider, apiKey: null);

        var result = await sutProvider.Sut.GetRangeAsync(_hashPrefix);

        Assert.IsType<ContentResult>(result);
    }

    [Theory]
    [BitAutoData(false, "Bitwarden")]
    [BitAutoData(true, "Bitwarden Self-Hosted")]
    public async Task GetRangeAsync_SetsUserAgentFromSelfHostedSetting(
        bool selfHosted,
        string expectedUserAgent,
        SutProvider<HibpController> sutProvider)
    {
        RespondToRangeRequestWith(HttpStatusCode.OK, _rangeData);
        ConfigureSut(sutProvider, selfHosted: selfHosted);

        await sutProvider.Sut.GetRangeAsync(_hashPrefix);

        var request = Assert.Single(_handler.CapturedRequests);
        Assert.Equal(expectedUserAgent, request.Headers.UserAgent.ToString());
    }

    [Theory]
    [BitAutoData(HttpStatusCode.NotFound)]
    [BitAutoData(HttpStatusCode.BadRequest)]
    [BitAutoData(HttpStatusCode.TooManyRequests)]
    [BitAutoData(HttpStatusCode.InternalServerError)]
    public async Task GetRangeAsync_WithUnsuccessfulResponse_ThrowsBadRequestException(
        HttpStatusCode statusCode,
        SutProvider<HibpController> sutProvider)
    {
        RespondToRangeRequestWith(statusCode);
        ConfigureSut(sutProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetRangeAsync(_hashPrefix));
        Assert.Equal($"Request failed. Status code: {statusCode}", exception.Message);

        // The range endpoint has no retry behavior, so a 429 must not be retried either.
        Assert.Single(_handler.CapturedRequests);
    }

    /// <summary>
    /// k-anonymity depends on only the first five characters of the SHA-1 hash leaving the server.
    /// The route constraint is what enforces that, so guard it against accidental removal.
    /// </summary>
    [Fact]
    public void GetRangeAsync_RouteConstrainsHashToFiveCharacters()
    {
        var attribute = typeof(HibpController)
            .GetMethod(nameof(HibpController.GetRangeAsync))
            .GetCustomAttribute<HttpGetAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("range/{hash:length(5)}", attribute.Template);
    }

    [Theory, BitAutoData]
    public async Task GetBreachAsync_WithMissingApiKey_ThrowsBadRequestException(
        SutProvider<HibpController> sutProvider,
        string username)
    {
        ConfigureSut(sutProvider, apiKey: null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetBreachAsync(username));
        Assert.Equal("HaveIBeenPwned API key not set.", exception.Message);
        Assert.Empty(_handler.CapturedRequests);
    }

    [Theory, BitAutoData]
    public async Task GetBreachAsync_WithNoBreaches_Returns200WithEmptyArray(
        SutProvider<HibpController> sutProvider,
        string username)
    {
        // The fallback responds 404, which HIBP uses to mean "account not pwned".
        ConfigureSut(sutProvider);

        var result = await sutProvider.Sut.GetBreachAsync(username);

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("[]", contentResult.Content);
        Assert.Equal("application/json", contentResult.ContentType);
    }

    [Theory, BitAutoData]
    public async Task GetBreachAsync_WithBreachesFound_Returns200WithBreachData(
        SutProvider<HibpController> sutProvider,
        string username)
    {
        var breachData = "[{\"Name\":\"Adobe\",\"Title\":\"Adobe\",\"Domain\":\"adobe.com\"}]";
        _handler.When(BreachUrl(username))
            .RespondWith(HttpStatusCode.OK)
            .WithContent("application/json", breachData);
        ConfigureSut(sutProvider);

        var result = await sutProvider.Sut.GetBreachAsync(username);

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(breachData, contentResult.Content);
        Assert.Equal("application/json", contentResult.ContentType);
    }

    [Theory, BitAutoData]
    public async Task GetBreachAsync_WhenRateLimited_RetriesOnce(
        SutProvider<HibpController> sutProvider,
        string username)
    {
        // CapturedRequests is appended to before matching, so this only matches the first request.
        _handler.When(_ => _handler.CapturedRequests.Count == 1)
            .RespondWith(HttpStatusCode.TooManyRequests)
            .WithHeader("retry-after", "0")
            .WithContent("application/json", string.Empty);
        ConfigureSut(sutProvider);

        var result = await sutProvider.Sut.GetBreachAsync(username);

        Assert.Equal(2, _handler.CapturedRequests.Count);
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("[]", contentResult.Content);
    }

    [Theory, BitAutoData]
    public async Task GetBreachAsync_WhenRateLimitedTwice_ThrowsBadRequestException(
        SutProvider<HibpController> sutProvider,
        string username)
    {
        _handler.When(BreachUrl(username))
            .RespondWith(HttpStatusCode.TooManyRequests)
            .WithHeader("retry-after", "0")
            .WithContent("application/json", string.Empty);
        ConfigureSut(sutProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetBreachAsync(username));
        Assert.Equal($"Request failed. Status code: {HttpStatusCode.TooManyRequests}", exception.Message);
        Assert.Equal(2, _handler.CapturedRequests.Count);
    }

    [Theory]
    [BitAutoData(HttpStatusCode.BadRequest)]
    [BitAutoData(HttpStatusCode.Unauthorized)]
    [BitAutoData(HttpStatusCode.InternalServerError)]
    public async Task GetBreachAsync_WithUnsuccessfulResponse_ThrowsBadRequestException(
        HttpStatusCode statusCode,
        SutProvider<HibpController> sutProvider,
        string username)
    {
        _handler.When(BreachUrl(username))
            .RespondWith(statusCode)
            .WithContent("application/json", string.Empty);
        ConfigureSut(sutProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetBreachAsync(username));
        Assert.Equal($"Request failed. Status code: {statusCode}", exception.Message);
        Assert.Single(_handler.CapturedRequests);
    }

    [Theory, BitAutoData]
    public async Task GetBreachAsync_EncodesUsername(
        SutProvider<HibpController> sutProvider)
    {
        ConfigureSut(sutProvider);

        await sutProvider.Sut.GetBreachAsync("test+user@example.com");

        var request = Assert.Single(_handler.CapturedRequests);
        Assert.Contains("test%2Buser%40example.com", request.RequestUri.ToString());
    }

    [Theory, BitAutoData]
    public async Task GetBreachAsync_IncludesRequiredHeaders(
        SutProvider<HibpController> sutProvider,
        string username,
        Guid userId)
    {
        ConfigureSut(sutProvider, userId: userId);

        await sutProvider.Sut.GetBreachAsync(username);

        var request = Assert.Single(_handler.CapturedRequests);
        Assert.Equal(_apiKey, Assert.Single(request.Headers.GetValues("hibp-api-key")));
        Assert.Equal("Bitwarden", request.Headers.UserAgent.ToString());

        // The client id is a SHA-256 of the user id so HIBP can rate limit per user without learning who they are.
        var expectedClientId = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(userId.ToByteArray()));
        Assert.Equal(expectedClientId, Assert.Single(request.Headers.GetValues("hibp-client-id")));
    }

    [Theory]
    [BitAutoData(false, "Bitwarden")]
    [BitAutoData(true, "Bitwarden Self-Hosted")]
    public async Task GetBreachAsync_SetsUserAgentFromSelfHostedSetting(
        bool selfHosted,
        string expectedUserAgent,
        SutProvider<HibpController> sutProvider,
        string username)
    {
        ConfigureSut(sutProvider, selfHosted: selfHosted);

        await sutProvider.Sut.GetBreachAsync(username);

        var request = Assert.Single(_handler.CapturedRequests);
        Assert.Equal(expectedUserAgent, request.Headers.UserAgent.ToString());
    }

    private void RespondToRangeRequestWith(HttpStatusCode statusCode, string content = "") =>
        _handler.When(_rangeUrl)
            .RespondWith(statusCode)
            .WithContent("text/plain", content);

    private static string BreachUrl(string username) =>
        $"https://haveibeenpwned.com/api/v3/breachedaccount/{WebUtility.UrlEncode(username)}" +
        "?truncateResponse=false&includeUnverified=false";

    /// <summary>
    /// Points the sut's <see cref="IHttpClientFactory"/> at <see cref="_handler"/> and applies the
    /// settings the controller reads. Call after configuring <see cref="_handler"/>.
    /// </summary>
    private SutProvider<HibpController> ConfigureSut(
        SutProvider<HibpController> sutProvider,
        string apiKey = _apiKey,
        bool selfHosted = false,
        Guid? userId = null)
    {
        // The controller uses the unnamed client, i.e. CreateClient(string.Empty).
        sutProvider.GetDependency<IHttpClientFactory>()
            .CreateClient(Arg.Any<string>())
            .Returns(_httpClient);

        var globalSettings = sutProvider.GetDependency<GlobalSettings>();
        globalSettings.HibpApiKey = apiKey;
        globalSettings.SelfHosted = selfHosted;

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId ?? Guid.NewGuid());

        return sutProvider;
    }
}
