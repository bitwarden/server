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
        _factory.SubstituteService<IDeleteTwoFactorWebAuthnCredentialCommand>(svc =>
            svc.DeleteTwoFactorWebAuthnCredentialAsync(default!, default).ReturnsForAnyArgs(true));
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
        await EnrollUserInWebAuthn();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        // DeleteWebAuthn removes a specific credential by Id, not the whole provider; the
        // delete command is substituted to return true, so this proves UV-token wiring
        // and route mounting end-to-end. Per-credential DELETE returns the updated parent
        // state in the body (the one DELETE on this controller that does), so 200 OK with
        // a nested "webAuthn" payload is expected here.
        var disableResponse = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/webauthn",
            new
            {
                Id = 0,
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        var deleteRoot = await ReadJsonRootAsync(disableResponse);
        Assert.Equal(JsonValueKind.Object, deleteRoot.GetProperty("webAuthn").ValueKind);
    }

    [Fact]
    public async Task GetWebAuthnChallenge_ValidSecret_ReturnsTokenUsableForPut()
    {
        var challengeResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn-challenge",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        var root = await ReadJsonRootAsync(challengeResponse);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("options").ValueKind);
        var uvToken = root.GetProperty("userVerificationToken").GetString()!;
        Assert.False(string.IsNullOrEmpty(uvToken));

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

    private Task EnrollUserInWebAuthn() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildWebAuthnProvidersJson());
}
