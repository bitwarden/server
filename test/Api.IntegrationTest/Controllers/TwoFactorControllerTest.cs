using System.Net;
using System.Text.Json;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Controllers.TwoFactor;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class TwoFactorControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _masterPasswordHash = "master_password_hash";
    private const string _authenticatorKey = "JBSWY3DPEHPK3PXP";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable> _authenticatorTokenFactory;
    private readonly IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> _userVerificationTokenFactory;

    private string _userEmail = null!;

    public TwoFactorControllerTest(ApiApplicationFactory factory)
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
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _authenticatorTokenFactory = _factory.GetService<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>();
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

    // ---------------------------------------------------------------------
    // YubiKey
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetYubiKey_ValidSecret_ReturnsTokenUsableForDelete()
    {
        await EnrollUserInYubiKey();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-yubikey",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "yubiKey");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/yubikey",
            new TwoFactorYubiKeyDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.YubiKey));
    }

    [Fact]
    public async Task PutYubiKey_ValidTokenAndPremium_UpdatesProvider()
    {
        await GrantPremium();
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-yubikey",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "yubiKey");

        var response = await _client.PutAsJsonAsync("/two-factor/yubikey",
            new TwoFactorYubiKeyUpdateRequestModel
            {
                Key1 = "ccccccccccbe",
                Nfc = true,
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.YubiKey));
    }

    // ---------------------------------------------------------------------
    // Duo (personal)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetDuo_ValidSecret_ReturnsTokenUsableForDelete()
    {
        await EnrollUserInDuo();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-duo",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/duo",
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
            new { MasterPasswordHash = _masterPasswordHash });
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
        var yubiKeyToken = ProtectUserVerificationToken(user, TwoFactorProviderType.YubiKey);

        var response = await SendJsonAsync(HttpMethod.Delete, "/two-factor/duo",
            new TwoFactorDuoDeleteRequestModel { UserVerificationToken = yubiKeyToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------
    // Organization Duo
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetOrganizationDuo_ValidSecret_ReturnsTokenUsableForDelete()
    {
        var (org, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _userEmail, passwordManagerSeats: 1);
        // Refresh the user's JWT so org-membership claims are present for ManagePolicies.
        await _loginHelper.LoginAsync(_userEmail);
        await EnrollOrganizationInDuo(org.Id);

        var getResponse = await _client.PostAsJsonAsync(
            $"/organizations/{org.Id}/two-factor/get-duo",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete,
            $"/organizations/{org.Id}/two-factor/duo",
            new TwoFactorOrganizationDuoDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshedOrg = await _organizationRepository.GetByIdAsync(org.Id);
        Assert.Null(refreshedOrg!.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo));
    }

    [Fact]
    public async Task PutOrganizationDuo_ValidToken_UpdatesProvider()
    {
        var (org, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _userEmail, passwordManagerSeats: 1);
        await _loginHelper.LoginAsync(_userEmail);
        var getResponse = await _client.PostAsJsonAsync(
            $"/organizations/{org.Id}/two-factor/get-duo",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "duo");

        var response = await _client.PutAsJsonAsync(
            $"/organizations/{org.Id}/two-factor/duo",
            new TwoFactorDuoUpdateRequestModel
            {
                ClientId = new string('a', 20),
                ClientSecret = new string('b', 40),
                Host = "api-test.duosecurity.com",
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshedOrg = await _organizationRepository.GetByIdAsync(org.Id);
        Assert.NotNull(refreshedOrg!.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo));
    }

    [Fact]
    public async Task DeleteOrganizationDuo_CrossProviderToken_BadRequest()
    {
        // Token-binding check runs before the ManagePolicies / org-membership check, so an
        // arbitrary org id is fine — the BadRequest fires first.
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var duoToken = ProtectUserVerificationToken(user, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(HttpMethod.Delete,
            $"/organizations/{Guid.NewGuid()}/two-factor/duo",
            new TwoFactorOrganizationDuoDeleteRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------
    // WebAuthn
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetWebAuthn_ValidSecret_ReturnsTokenUsableForDelete()
    {
        await EnrollUserInWebAuthn();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-webauthn",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        // DeleteWebAuthn removes a specific credential by Id, not the whole provider; the
        // delete command is substituted to return true, so this proves UV-token wiring
        // and route mounting end-to-end. Per-credential DELETE returns the updated parent
        // state in the body (the one DELETE on this controller that does), so 200 OK with
        // a nested "webAuthn" payload is expected here.
        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/webauthn",
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
            new { MasterPasswordHash = _masterPasswordHash });
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
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/webauthn/all",
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
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "webAuthn");

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/webauthn/all",
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

        var response = await SendJsonAsync(HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = expiredToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteWebAuthnAll_CrossProviderToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        // Token bound to Duo replayed against the WebAuthn-all DELETE endpoint.
        var duoToken = ProtectUserVerificationToken(user, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------
    // Email
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetEmail_ValidSecret_ReturnsTokenUsableForDelete()
    {
        await EnrollUserInEmail();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "email");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Email));
    }

    [Fact]
    public async Task DeleteEmail_CrossProviderToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var duoToken = ProtectUserVerificationToken(user, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PutEmail_ValidTokenAndCode_UpdatesProvider()
    {
        // Full enrollment chain: GET mints the UV token, SendEmailSetup validates it and triggers
        // the email service, and PUT consumes the UV token + OTP.
        //
        // The OTP itself cannot be intercepted from the substituted email service — production
        // computes it inside SendTwoFactorSetupEmailAsync (from SecurityStamp + Email metadata
        // + time window) and ships it out of band via the user's inbox. SendEmailSetup also doesn't
        // persist the Email metadata it temporarily attaches via model.ToUser, so a fresh fetch
        // post-SendEmailSetup has no metadata for OTP generation.
        //
        // The test reproduces the OTP locally by applying the same in-memory mutation SendEmailSetup
        // applied (Email metadata = _userEmail) and asking UserManager to generate a token. The
        // resulting OTP matches what PutEmail will validate because PutEmail's server-side flow
        // applies the same mutation against the same SecurityStamp before checking.
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "email");

        var sendResponse = await _client.PostAsJsonAsync("/two-factor/send-email",
            new
            {
                Email = _userEmail,
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);

        var userManager = _factory.GetService<UserManager<User>>();
        var userForOtp = (await _userRepository.GetByEmailAsync(_userEmail))!;
        userForOtp.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = _userEmail.ToLowerInvariant() },
                Enabled = true,
            },
        });
        var emailOtp = await userManager.GenerateTwoFactorTokenAsync(userForOtp,
            CoreHelpers.CustomProviderName(TwoFactorProviderType.Email));

        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new TwoFactorEmailUpdateRequestModel
            {
                Email = _userEmail,
                Token = emailOtp,
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var afterPut = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(afterPut!.GetTwoFactorProvider(TwoFactorProviderType.Email));
    }

    [Fact]
    public async Task SendEmailSetup_ValidToken_InvokesEmailService()
    {
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "email");
        var emailService = _factory.GetService<ITwoFactorEmailService>();

        var response = await _client.PostAsJsonAsync("/two-factor/send-email",
            new
            {
                Email = _userEmail,
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await emailService.Received().SendTwoFactorSetupEmailAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task SendEmailSetup_MissingUserVerificationToken_BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/two-factor/send-email",
            new { Email = _userEmail });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendEmailSetup_MissingEmail_BadRequest()
    {
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = _masterPasswordHash });
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "email");

        var response = await _client.PostAsJsonAsync("/two-factor/send-email",
            new { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutEmail_MissingUserVerificationToken_BadRequest()
    {
        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new { Email = _userEmail, Token = "123456" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutEmail_MissingToken_BadRequest()
    {
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = _masterPasswordHash });
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "email");

        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new { Email = _userEmail, UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendEmailLogin_ValidMasterPassword_InvokesEmailService()
    {
        var emailService = _factory.GetService<ITwoFactorEmailService>();

        var response = await _client.PostAsJsonAsync("/two-factor/send-email-login",
            new
            {
                Email = _userEmail,
                MasterPasswordHash = _masterPasswordHash,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await emailService.Received().SendTwoFactorEmailAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task SendEmailLogin_NoCredentials_BadRequest()
    {
        // Body carries only Email — no MasterPasswordHash, OTP, AuthRequestAccessCode,
        // or SsoEmail2FaSessionToken. The model's Validate must reject before the controller runs.
        var response = await _client.PostAsJsonAsync("/two-factor/send-email-login",
            new { Email = _userEmail });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // Cross-cutting: provider-type binding + legacy endpoint removal
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteYubiKey_CrossProviderToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        // Token bound to Duo replayed against the YubiKey DELETE endpoint.
        var duoToken = ProtectUserVerificationToken(user, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(HttpMethod.Delete, "/two-factor/yubikey",
            new TwoFactorYubiKeyDeleteRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private string ProtectUserVerificationToken(User user, TwoFactorProviderType providerType) =>
        TwoFactorIntegrationTestHelpers.ProtectUserVerificationToken(_userVerificationTokenFactory, user, providerType);

    private Task EnrollUserInAuthenticator() =>
        TwoFactorIntegrationTestHelpers.SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, TwoFactorIntegrationTestHelpers.BuildAuthenticatorProvidersJson(_authenticatorKey));

    private Task EnrollUserInYubiKey() =>
        TwoFactorIntegrationTestHelpers.SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, TwoFactorIntegrationTestHelpers.BuildYubiKeyProvidersJson());

    private Task EnrollUserInDuo() =>
        TwoFactorIntegrationTestHelpers.SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, TwoFactorIntegrationTestHelpers.BuildDuoProvidersJson());

    private Task EnrollUserInEmail() =>
        TwoFactorIntegrationTestHelpers.SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, TwoFactorIntegrationTestHelpers.BuildEmailProvidersJson(_userEmail));

    private Task EnrollUserInWebAuthn() =>
        TwoFactorIntegrationTestHelpers.SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, TwoFactorIntegrationTestHelpers.BuildWebAuthnProvidersJson());

    private Task EnrollOrganizationInDuo(Guid organizationId) =>
        TwoFactorIntegrationTestHelpers.SetOrganizationTwoFactorProvidersJsonAsync(
            _organizationRepository, organizationId, TwoFactorIntegrationTestHelpers.BuildOrganizationDuoProvidersJson());

    private Task SetUserTwoFactorProvidersJson(string providersJson) =>
        TwoFactorIntegrationTestHelpers.SetUserTwoFactorProvidersJsonAsync(_userRepository, _userEmail, providersJson);

    private Task GrantPremium() =>
        TwoFactorIntegrationTestHelpers.GrantPremiumAsync(_userRepository, _userEmail);

    private static Task<(bool Enabled, string UserVerificationToken)> ReadEnabledAndUserVerificationTokenAsync(
        HttpResponseMessage response, string providerKey) =>
        TwoFactorIntegrationTestHelpers.ReadEnabledAndUserVerificationTokenAsync(response, providerKey);

    private static Task<JsonElement> ReadJsonRootAsync(HttpResponseMessage response) =>
        TwoFactorIntegrationTestHelpers.ReadJsonRootAsync(response);

    private Task<HttpResponseMessage> SendJsonAsync<T>(HttpMethod method, string url, T body) =>
        TwoFactorIntegrationTestHelpers.SendJsonAsync(_client, method, url, body);

    private Task<HttpResponseMessage> SendRawJsonAsync(HttpMethod method, string url, string json) =>
        TwoFactorIntegrationTestHelpers.SendRawJsonAsync(_client, method, url, json);
}
