using System.Net;
using System.Text.Json;
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

public class TwoFactorControllerWebAuthnTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> _userVerificationTokenFactory;

    private string _userEmail = null!;

    public TwoFactorControllerWebAuthnTests(ApiApplicationFactory factory)
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
    public async Task GetWebAuthn_ValidSecret_ReturnsTokenUsableForDelete()
    {
        // Seed two credentials so per-credential DELETE succeeds against the real
        // DeleteTwoFactorWebAuthnCredentialCommand (which refuses to remove the final credential
        // to prevent lockout — that refusal path has its own test below).
        await EnrollUserInWebAuthnWithTwoCredentials();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        // Per-credential DELETE returns the updated parent state in the body (the one DELETE on
        // this controller that does), so 200 OK with a nested "webAuthn" payload is expected here.
        var disableResponse = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn",
            new
            {
                Id = 0,
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        var deleteRoot = await ReadJsonRootAsync(disableResponse);
        Assert.Equal(JsonValueKind.Object, deleteRoot.GetProperty("webAuthn").ValueKind);

        // Key0 was removed, Key1 remains — the "refuse last credential" guard didn't fire because
        // there was more than one credential seeded.
        var refreshed = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var remaining = refreshed.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn)!;
        Assert.False(remaining.MetaData.ContainsKey("Key0"));
        Assert.True(remaining.MetaData.ContainsKey("Key1"));
    }

    [Fact]
    public async Task GetWebAuthnChallenge_ValidToken_ReturnsOptionsForPut()
    {
        // get-webauthn mints the UV token; get-webauthn-challenge replays it (no new mint)
        // and returns the FIDO2 registration options; PUT replays the same token again with
        // the DeviceResponse to complete enrollment.
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");
        Assert.False(string.IsNullOrEmpty(uvToken));

        var challengeResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn-challenge",
            new TwoFactorWebAuthnChallengeRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        var challengeRoot = await ReadJsonRootAsync(challengeResponse);
        Assert.Equal(JsonValueKind.Object, challengeRoot.GetProperty("options").ValueKind);

        var putResponse = await _client.PutAsJsonAsync("/two-factor/webauthn",
            new
            {
                Id = 0,
                Name = "TestKey",
                DeviceResponse = new { },
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
    }

    [Fact]
    public async Task GetWebAuthnChallenge_ExpiredToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var expiredToken = _userVerificationTokenFactory.Protect(new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.WebAuthn,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var response = await _client.PostAsJsonAsync("/two-factor/get-webauthn-challenge",
            new TwoFactorWebAuthnChallengeRequestModel { UserVerificationToken = expiredToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetWebAuthnChallenge_CrossProviderToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        // Token bound to Duo replayed against the WebAuthn challenge endpoint.
        var duoToken = ProtectUserVerificationToken(_userVerificationTokenFactory, user, TwoFactorProviderType.Duo);

        var response = await _client.PostAsJsonAsync("/two-factor/get-webauthn-challenge",
            new TwoFactorWebAuthnChallengeRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteWebAuthnAll_ValidToken_RemovesProvider()
    {
        await EnrollUserInWebAuthn();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn));
    }

    [Fact]
    public async Task DeleteWebAuthnAll_SingleCredentialEnrollment_RemovesProvider()
    {
        // The per-credential DELETE /two-factor/webauthn refuses the last registered credential
        // (lockout prevention in DeleteTwoFactorWebAuthnCredentialCommand). The bulk-disable
        // endpoint is the only path that handles "user has exactly one WebAuthn credential and
        // wants to disable WebAuthn entirely" correctly.
        await EnrollUserInWebAuthn(); // single-credential enrollment

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");

        var disableResponse = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn));
    }

    [Fact]
    public async Task DeleteWebAuthnAll_ExpiredToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var expiredToken = _userVerificationTokenFactory.Protect(new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.WebAuthn,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = expiredToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteWebAuthnAll_CrossProviderToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        // Token bound to Duo replayed against the WebAuthn-all DELETE endpoint.
        var duoToken = ProtectUserVerificationToken(_userVerificationTokenFactory, user, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteWebAuthnAll_WrongUserToken_BadRequest()
    {
        // Mint a token bound to a different (unrelated) user, then replay it against the current
        // authenticated principal's endpoint. The token unprotects but TokenIsValid(user) fails
        // because UserId doesn't match.
        var otherUserEmail = $"two-factor-other-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(otherUserEmail);
        var otherUser = (await _userRepository.GetByEmailAsync(otherUserEmail))!;
        var tokenForOtherUser = ProtectUserVerificationToken(
            _userVerificationTokenFactory, otherUser, TwoFactorProviderType.WebAuthn);

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = tokenForOtherUser });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteWebAuthnAll_TamperedToken_BadRequest()
    {
        // A random string can't be unprotected by the data protector; TryUnprotect returns false
        // and TwoFactorUserVerificationTokenable.Validate short-circuits to false.
        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteWebAuthn_LastCredential_BadRequest()
    {
        // With exactly one credential enrolled, per-credential DELETE must refuse (the guard that
        // prevents accidental lockout — /webauthn/all is the sanctioned path for removing the last).
        await EnrollUserInWebAuthn();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn",
            new
            {
                Id = 0,
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Unable to delete WebAuthn credential.", await response.Content.ReadAsStringAsync());

        var refreshed = (await _userRepository.GetByEmailAsync(_userEmail))!;
        Assert.NotNull(refreshed.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn));
    }

    private Task EnrollUserInWebAuthn() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildWebAuthnProvidersJson());

    private Task EnrollUserInWebAuthnWithTwoCredentials() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildWebAuthnProvidersJson(credentialCount: 2));
}
