using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Api.Auth.Models.Request;
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
using OtpNet;
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
    // Authenticator
    // ---------------------------------------------------------------------

    [Fact]
    public async Task PutAuthenticator_ExpiredToken_BadRequest()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        var expiredToken = _authenticatorTokenFactory.Protect(
            new TwoFactorAuthenticatorUserVerificationTokenable(user!, _authenticatorKey)
            {
                ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
            });

        var response = await _client.PutAsJsonAsync("/two-factor/authenticator",
            new TwoFactorAuthenticatorUpdateRequestModel
            {
                Token = "123456",
                Key = _authenticatorKey,
                UserVerificationToken = expiredToken,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteAuthenticator_ExpiredToken_BadRequest()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        var expiredToken = _authenticatorTokenFactory.Protect(
            new TwoFactorAuthenticatorUserVerificationTokenable(user!, _authenticatorKey)
            {
                ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
            });

        var response = await SendJsonAsync(HttpMethod.Delete, "/two-factor/authenticator",
            new TwoFactorAuthenticatorDeleteRequestModel
            {
                Key = _authenticatorKey,
                UserVerificationToken = expiredToken,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetAuthenticator_ValidSecret_ReturnsTokenUsableForDelete()
    {
        await EnrollUserInAuthenticator();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-authenticator",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var root = await ReadJsonRootAsync(getResponse);
        Assert.True(root.GetProperty("enabled").GetBoolean());
        var key = root.GetProperty("key").GetString()!;
        var uvToken = root.GetProperty("userVerificationToken").GetString()!;

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/authenticator",
            new TwoFactorAuthenticatorDeleteRequestModel
            {
                Key = key,
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Authenticator));
    }

    [Fact]
    public async Task PutAuthenticator_ValidTokenAndCode_UpdatesProvider()
    {
        // GET mints a fresh key + UV token; compute the TOTP off that key and PUT.
        // AuthenticatorTokenProvider is verify-only (GenerateAsync returns null); compute the
        // TOTP with OtpNet, which is the same library the provider validates against.
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-authenticator",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var root = await ReadJsonRootAsync(getResponse);
        var key = root.GetProperty("key").GetString()!;
        var uvToken = root.GetProperty("userVerificationToken").GetString()!;
        var totp = new Totp(Base32Encoding.ToBytes(key)).ComputeTotp();

        var response = await _client.PutAsJsonAsync("/two-factor/authenticator",
            new TwoFactorAuthenticatorUpdateRequestModel
            {
                Token = totp,
                Key = key,
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Authenticator));
    }

    [Fact]
    public async Task DeleteAuthenticator_BodyTypeMismatch_RespectsUrlRoute()
    {
        // Any "Type" field sent in the wire body must be ignored — the URL determines the
        // provider. Enroll Authenticator and Email, then send a body claiming Type=Email
        // to /authenticator and assert only Authenticator is touched.
        await EnrollUserInAuthenticator();
        await EnrollUserInEmail();

        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var authToken = _authenticatorTokenFactory.Protect(
            new TwoFactorAuthenticatorUserVerificationTokenable(user, _authenticatorKey));

        var body = new JsonObject
        {
            ["Type"] = (int)TwoFactorProviderType.Email,
            ["Key"] = _authenticatorKey,
            ["UserVerificationToken"] = authToken,
        };

        var response = await SendRawJsonAsync(HttpMethod.Delete, "/two-factor/authenticator", body.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = (await _userRepository.GetByEmailAsync(_userEmail))!;
        Assert.Null(refreshed.GetTwoFactorProvider(TwoFactorProviderType.Authenticator));
        Assert.NotNull(refreshed.GetTwoFactorProvider(TwoFactorProviderType.Email));
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
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/yubikey",
            new TwoFactorYubiKeyDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

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
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);

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
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/duo",
            new TwoFactorDuoDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

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
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);

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
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete,
            $"/organizations/{org.Id}/two-factor/duo",
            new TwoFactorOrganizationDuoDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

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
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);

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
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        // DeleteWebAuthn removes a specific credential by Id, not the whole provider; the
        // delete command is substituted to return true, so this proves UV-token wiring
        // and route mounting end-to-end.
        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/webauthn",
            new
            {
                Id = 0,
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
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
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

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
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/webauthn/all",
            new TwoFactorWebAuthnDeleteAllRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

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
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Email));
    }

    [Fact]
    public async Task PutEmail_ValidTokenAndCode_UpdatesProvider()
    {
        // Full enrollment chain: GET mints the UV token, SendEmail validates it and triggers
        // the email service, and PUT consumes the UV token + OTP.
        //
        // The OTP itself cannot be intercepted from the substituted email service — production
        // computes it inside SendTwoFactorSetupEmailAsync (from SecurityStamp + Email metadata
        // + time window) and ships it out of band via the user's inbox. SendEmail also doesn't
        // persist the Email metadata it temporarily attaches via model.ToUser, so a fresh fetch
        // post-SendEmail has no metadata for OTP generation.
        //
        // The test reproduces the OTP locally by applying the same in-memory mutation SendEmail
        // applied (Email metadata = _userEmail) and asking UserManager to generate a token. The
        // resulting OTP matches what PutEmail will validate because PutEmail's server-side flow
        // applies the same mutation against the same SecurityStamp before checking.
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);

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
    public async Task SendEmail_ValidToken_InvokesEmailService()
    {
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = _masterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);
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
    public async Task SendEmail_MissingUserVerificationToken_BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/two-factor/send-email",
            new { Email = _userEmail });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendEmail_MissingEmail_BadRequest()
    {
        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = _masterPasswordHash });
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);

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
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse);

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
        _userVerificationTokenFactory.Protect(new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = providerType,
            ExpirationDate = DateTime.UtcNow.AddMinutes(30),
        });

    private async Task EnrollUserInAuthenticator() =>
        await SetUserTwoFactorProvidersJson(
            $"{{\"0\":{{\"Enabled\":true,\"MetaData\":{{\"Key\":\"{_authenticatorKey}\"}}}}}}");

    private async Task EnrollUserInYubiKey() =>
        await SetUserTwoFactorProvidersJson(
            "{\"3\":{\"Enabled\":true,\"MetaData\":{\"Key1\":\"ccccccccccbe\",\"Nfc\":true}}}");

    private async Task EnrollUserInDuo() =>
        await SetUserTwoFactorProvidersJson(
            "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"" + new string('s', 40)
            + "\",\"ClientId\":\"" + new string('c', 20) + "\",\"Host\":\"api-test.duosecurity.com\"}}}");

    private async Task EnrollUserInEmail() =>
        await SetUserTwoFactorProvidersJson(
            "{\"1\":{\"Enabled\":true,\"MetaData\":{\"Email\":\"" + _userEmail + "\"}}}");

    private async Task EnrollUserInWebAuthn() =>
        await SetUserTwoFactorProvidersJson(
            "{\"7\":{\"Enabled\":true,\"MetaData\":{\"Key0\":{\"Name\":\"TestKey\",\"Descriptor\":{\"Id\":\"AAAA\",\"Type\":0,\"Transports\":null},\"PublicKey\":\"AAAA\",\"UserHandle\":\"AAAA\",\"SignatureCounter\":0,\"RegDate\":\"2024-01-01T00:00:00\",\"Migrated\":false,\"AaGuid\":\"00000000-0000-0000-0000-000000000000\"}}}}");

    private async Task EnrollOrganizationInDuo(Guid organizationId)
    {
        var org = (await _organizationRepository.GetByIdAsync(organizationId))!;
        org.TwoFactorProviders =
            "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"" + new string('s', 40)
            + "\",\"ClientId\":\"" + new string('c', 20) + "\",\"Host\":\"api-test.duosecurity.com\"}}}";
        await _organizationRepository.UpsertAsync(org);
    }

    private async Task SetUserTwoFactorProvidersJson(string providersJson)
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        user.TwoFactorProviders = providersJson;
        await _userRepository.UpsertAsync(user);
    }

    private async Task GrantPremium()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        user.Premium = true;
        await _userRepository.UpsertAsync(user);
    }

    // Response models (e.g., TwoFactorWebAuthnResponseModel, TwoFactorAuthenticatorResponseModel)
    // declare a parameterized constructor that System.Text.Json cannot map for deserialization.
    // Read the fields the tests care about structurally instead.
    private static async Task<(bool Enabled, string UserVerificationToken)> ReadEnabledAndUserVerificationTokenAsync(
        HttpResponseMessage response)
    {
        var root = await ReadJsonRootAsync(response);
        return (
            root.GetProperty("enabled").GetBoolean(),
            root.GetProperty("userVerificationToken").GetString() ?? string.Empty);
    }

    private static async Task<JsonElement> ReadJsonRootAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private Task<HttpResponseMessage> SendJsonAsync<T>(HttpMethod method, string url, T body) =>
        SendRawJsonAsync(method, url, JsonSerializer.Serialize(body));

    private async Task<HttpResponseMessage> SendRawJsonAsync(HttpMethod method, string url, string json)
    {
        using var message = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return await _client.SendAsync(message);
    }
}
