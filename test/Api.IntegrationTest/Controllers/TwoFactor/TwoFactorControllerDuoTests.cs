using System.Net;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Controllers.TwoFactor.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Controllers.TwoFactor;

public class TwoFactorControllerDuoTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> _userVerificationTokenFactory;

    private string _userEmail = null!;

    public TwoFactorControllerDuoTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IDuoUniversalTokenService>(svc =>
            svc.ValidateDuoConfiguration(default, default, default).ReturnsForAnyArgs(true));
        _factory.SubstituteService<ICompleteTwoFactorWebAuthnRegistrationCommand>(svc =>
            svc.CompleteTwoFactorWebAuthnRegistrationAsync(default!, default, default!, default!).ReturnsForAnyArgs(true));
        _factory.SubstituteService<ITwoFactorEmailService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _userVerificationTokenFactory = _factory.GetService<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>();
    }

    public async Task InitializeAsync()
    {
        _userEmail = $"two-factor-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_userEmail);
        await _loginHelper.LoginAsync(_userEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetDuo_ValidSecret_ReturnsTokenUsableForDelete()
    {
        await EnrollUserInDuo();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-duo",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/duo",
            new TwoFactorDuoDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Duo));
    }

    [Fact]
    public async Task PutDuo_ValidTokenAndPremium_UpdatesProvider()
    {
        await GrantPremium();
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-duo",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");

        var response = await _client.PutAsJsonAsync("/two-factor/duo",
            new TwoFactorDuoUpdateRequestModel
            {
                ClientId = new string('a', 20),
                ClientSecret = new string('b', 40),
                Host = "api-test.duosecurity.com",
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Duo));
    }

    [Fact]
    public async Task DeleteDuo_CrossProviderToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var yubiKeyToken = ProtectUserVerificationToken(_userVerificationTokenFactory, user, TwoFactorProviderType.YubiKey);

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/duo",
            new TwoFactorDuoDeleteRequestModel { UserVerificationToken = yubiKeyToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteDuo_WrongUserToken_BadRequest()
    {
        // Token minted for a different user and replayed by the current authenticated principal.
        // TokenIsValid pins UserId, so the token unprotects but fails validation.
        var otherUserEmail = $"two-factor-other-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(otherUserEmail);
        var otherUser = (await _userRepository.GetByEmailAsync(otherUserEmail))!;
        var tokenForOtherUser = ProtectUserVerificationToken(
            _userVerificationTokenFactory, otherUser, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/duo",
            new TwoFactorDuoDeleteRequestModel { UserVerificationToken = tokenForOtherUser });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PutDuo_WrongUserToken_BadRequest()
    {
        // No GrantPremium() needed — UV token validation runs before the premium check, so this
        // test never reaches the premium branch.
        var otherUserEmail = $"two-factor-other-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(otherUserEmail);
        var otherUser = (await _userRepository.GetByEmailAsync(otherUserEmail))!;
        var tokenForOtherUser = ProtectUserVerificationToken(
            _userVerificationTokenFactory, otherUser, TwoFactorProviderType.Duo);

        var response = await _client.PutAsJsonAsync("/two-factor/duo",
            new TwoFactorDuoUpdateRequestModel
            {
                ClientId = new string('a', 20),
                ClientSecret = new string('b', 40),
                Host = "api-test.duosecurity.com",
                UserVerificationToken = tokenForOtherUser,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteDuo_TamperedToken_BadRequest()
    {
        // Garbage cipher text can't be unprotected; TryUnprotect returns false and Validate
        // short-circuits.
        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/duo",
            new TwoFactorDuoDeleteRequestModel { UserVerificationToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PutDuo_NoPremium_BadRequest()
    {
        // Deliberately skip GrantPremium — PutDuo enforces premium after UV token validation.
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-duo",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");

        var response = await _client.PutAsJsonAsync("/two-factor/duo",
            new TwoFactorDuoUpdateRequestModel
            {
                ClientId = new string('a', 20),
                ClientSecret = new string('b', 40),
                Host = "api-test.duosecurity.com",
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Premium status is required.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteDuo_ValidTokenReusedTwice_BothSucceed()
    {
        // The UV token is intentionally non-single-use — a still-valid token can be replayed
        // for the lifetime of its expiration window. This locks that behavior in so a future
        // change to single-use semantics has to update this assertion consciously.
        await EnrollUserInDuo();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-duo",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");

        var firstDelete = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/duo",
            new TwoFactorDuoDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, firstDelete.StatusCode);

        // Re-enroll and replay the same UV token. It's still within its 30-minute expiration and
        // remains bound to (user, Duo), so validation still passes.
        await EnrollUserInDuo();
        var secondDelete = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/duo",
            new TwoFactorDuoDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, secondDelete.StatusCode);
    }

    private Task EnrollUserInDuo() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildDuoProvidersJson());

    private Task GrantPremium() =>
        GrantPremiumAsync(_userRepository, _userEmail);
}
