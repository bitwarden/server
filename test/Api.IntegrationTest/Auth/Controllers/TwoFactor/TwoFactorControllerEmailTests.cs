using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Core.Auth.Enums;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Auth.Controllers.TwoFactor;

/// <summary>
/// Covers the email two-factor endpoints of <c>TwoFactorController</c> with the real two-factor email service.
/// Only the mail service is substituted, so tests read the emailed code from it. The API host shares the
/// Identity host's persistent cache, so a login code emailed by the API can be redeemed against the Identity
/// token endpoint, as it is in a deployed environment.
/// </summary>
/// <remarks>
/// Login requests identify their device the way shipped clients do: the <c>Device-Identifier</c> header, a
/// <c>DeviceIdentifier</c> body field, or both.
/// </remarks>
public class TwoFactorControllerEmailTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string DeviceIdentifier = "two-factor-email-device";
    private const string OtherDeviceIdentifier = "two-factor-email-other-device";
    private const string AuthRequestAccessCode = "auth-request-access-code";
    private const string SsoRedirectUri = "https://localhost:8080/sso-connector.html";
    private const string InvalidTwoFactorTokenMessage = "Two-step token is invalid. Try again.";
    private const string CannotSendTwoFactorEmailMessage = "Cannot send two-factor email.";
    private const string DeviceIdentifierRequiredMessage = "A valid device identifier is required.";
    private const string InvalidSsoSessionTokenMessage =
        "a valid, non-expired SSO Email 2FA Session token is required to send 2FA emails.";

    /// <summary>
    /// Authorization codes the substituted Identity code store hands out, keyed by code. The store is created
    /// once per class fixture, so each SSO test registers its own uniquely named code here.
    /// </summary>
    private static readonly ConcurrentDictionary<string, AuthorizationCode> _ssoAuthorizationCodes = new();

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> _userVerificationTokenFactory;

    private string _userEmail = null!;

    public TwoFactorControllerEmailTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IDuoUniversalTokenService>(svc =>
            svc.ValidateDuoConfiguration(default, default, default).ReturnsForAnyArgs(true));
        _factory.SubstituteService<ICompleteTwoFactorWebAuthnRegistrationCommand>(svc =>
            svc.CompleteTwoFactorWebAuthnRegistrationAsync(default!, default, default!, default!).ReturnsForAnyArgs(true));
        _factory.SubstituteService<IMailService>(_ => { });
        _factory.ShareIdentityPersistentCache();
        _factory.Identity.SubstituteService<IAuthorizationCodeStore>(store =>
            store.GetAuthorizationCodeAsync(Arg.Any<string>())
                .Returns(call => Task.FromResult(_ssoAuthorizationCodes.GetValueOrDefault(call.Arg<string>()))));
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _mailService = _factory.GetService<IMailService>();
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

    // ---------------------------------------------------------------------
    // get-email and delete
    // ---------------------------------------------------------------------

    /// <summary>
    /// get-email reports the provider as enabled and returns a user verification token that delete accepts.
    /// </summary>
    [Fact]
    public async Task GetEmail_ValidSecret_ReturnsTokenUsableForDelete()
    {
        await EnrollUserInEmail();

        var getResponse = await _client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (enabled, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "email");
        Assert.True(enabled);
        Assert.False(string.IsNullOrEmpty(uvToken));

        var disableResponse = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var refreshed = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.Null(refreshed!.GetTwoFactorProvider(TwoFactorProviderType.Email));
    }

    /// <summary>
    /// Delete rejects a user verification token minted for a different provider.
    /// </summary>
    [Fact]
    public async Task DeleteEmail_CrossProviderToken_BadRequest()
    {
        var user = await GetUserAsync();
        var duoToken = ProtectUserVerificationToken(_userVerificationTokenFactory, user, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Delete rejects a user verification token minted for a different user.
    /// </summary>
    [Fact]
    public async Task DeleteEmail_WrongUserToken_BadRequest()
    {
        var otherUserEmail = $"two-factor-other-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(otherUserEmail);
        var otherUser = (await _userRepository.GetByEmailAsync(otherUserEmail))!;
        var tokenForOtherUser = ProtectUserVerificationToken(
            _userVerificationTokenFactory, otherUser, TwoFactorProviderType.Email);

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = tokenForOtherUser });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Delete rejects a user verification token that cannot be unprotected.
    /// </summary>
    [Fact]
    public async Task DeleteEmail_TamperedToken_BadRequest()
    {
        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------
    // Setup: send-email and PUT email
    // ---------------------------------------------------------------------

    /// <summary>
    /// Full setup: send-email emails a setup code to the new address, and PUT with that code enables the provider.
    /// </summary>
    [Fact]
    public async Task PutEmail_CodeFromSendEmail_EnablesProvider()
    {
        var uvToken = await GetEmailUserVerificationTokenAsync();

        var sendResponse = await _client.PostAsJsonAsync("/two-factor/send-email",
            new { Email = _userEmail, UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Setup);

        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new TwoFactorEmailUpdateRequestModel { Email = _userEmail, Token = code, UserVerificationToken = uvToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var provider = (await GetUserAsync()).GetTwoFactorProvider(TwoFactorProviderType.Email);
        Assert.NotNull(provider);
        Assert.True(provider.Enabled);
    }

    /// <summary>
    /// Setup for an address other than the account email: send-email emails the setup code to that address, and
    /// PUT with the code enables the provider for it.
    /// </summary>
    [Fact]
    public async Task PutEmail_CodeSentToDifferentAddress_EnablesProviderForThatAddress()
    {
        var twoFactorAddress = $"two-factor-inbox-{Guid.NewGuid()}@bitwarden.com";
        var uvToken = await GetEmailUserVerificationTokenAsync();

        var sendResponse = await _client.PostAsJsonAsync("/two-factor/send-email",
            new { Email = twoFactorAddress, UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var emailed = FindLatestEmailedTwoFactorCode(_mailService, _userEmail);
        Assert.NotNull(emailed);
        Assert.Equal(TwoFactorEmailPurpose.Setup, emailed.Purpose);
        Assert.Equal(twoFactorAddress, emailed.Recipient);

        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new TwoFactorEmailUpdateRequestModel
            {
                Email = twoFactorAddress,
                Token = emailed.Code,
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var provider = (await GetUserAsync()).GetTwoFactorProvider(TwoFactorProviderType.Email);
        Assert.NotNull(provider);
        Assert.Equal(twoFactorAddress, provider.MetaData["Email"]);
    }

    /// <summary>
    /// PUT with a code other than the one emailed by send-email is rejected and leaves the provider off.
    /// </summary>
    [Fact]
    public async Task PutEmail_WrongCode_BadRequestAndProviderNotEnabled()
    {
        var uvToken = await GetEmailUserVerificationTokenAsync();
        var sendResponse = await _client.PostAsJsonAsync("/two-factor/send-email",
            new { Email = _userEmail, UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Setup);

        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new TwoFactorEmailUpdateRequestModel
            {
                Email = _userEmail,
                Token = WrongCodeFor(code),
                UserVerificationToken = uvToken,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid token.", await response.Content.ReadAsStringAsync());
        Assert.Null((await GetUserAsync()).GetTwoFactorProvider(TwoFactorProviderType.Email));
    }

    /// <summary>
    /// send-email requires a user verification token.
    /// </summary>
    [Fact]
    public async Task SendEmailSetup_MissingUserVerificationToken_BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/two-factor/send-email",
            new { Email = _userEmail });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// send-email requires the email address to set up.
    /// </summary>
    [Fact]
    public async Task SendEmailSetup_MissingEmail_BadRequest()
    {
        var uvToken = await GetEmailUserVerificationTokenAsync();

        var response = await _client.PostAsJsonAsync("/two-factor/send-email",
            new { UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// PUT requires a user verification token.
    /// </summary>
    [Fact]
    public async Task PutEmail_MissingUserVerificationToken_BadRequest()
    {
        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new { Email = _userEmail, Token = "123456" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// PUT requires the emailed code.
    /// </summary>
    [Fact]
    public async Task PutEmail_MissingToken_BadRequest()
    {
        var uvToken = await GetEmailUserVerificationTokenAsync();

        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new { Email = _userEmail, UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // send-email-login
    // ---------------------------------------------------------------------

    /// <summary>
    /// The request as the CLI sends it: master password, with the device identifier in the <c>Device-Identifier</c>
    /// header only. The emailed code logs the device in.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_MasterPasswordAndDeviceHeader_EmailsCodeThatLogsIn()
    {
        await EnrollUserInEmail();
        await ChallengeAsync(DeviceIdentifier);

        var response = await SendEmailLoginAsync(
            MasterPasswordBody(_userEmail), headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Login);
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, code));
    }

    // TODO: PM-44555 - When the body fallback is removed, a request without the header is rejected; change this
    // test to expect a 400, or delete it.
    /// <summary>
    /// The request as the mobile apps send it: master password, with the device identifier in the body only and the
    /// unused SSO token field sent as null. The emailed code logs the device in.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_MasterPasswordAndDeviceInBodyOnly_EmailsCodeThatLogsIn()
    {
        await EnrollUserInEmail();
        await ChallengeAsync(DeviceIdentifier);
        var body = MasterPasswordBody(_userEmail, DeviceIdentifier);
        body["SsoEmail2FaSessionToken"] = null;

        var response = await SendEmailLoginAsync(body, headerDeviceIdentifier: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Login);
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, code));
    }

    /// <summary>
    /// The request as the web, browser, and desktop clients send it: master password, with the same device identifier
    /// in the header and the body, and the unused credential fields sent as empty strings. The emailed code logs the
    /// device in.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_MasterPasswordAndDeviceInHeaderAndBody_EmailsCodeThatLogsIn()
    {
        await EnrollUserInEmail();
        await ChallengeAsync(DeviceIdentifier);
        var body = MasterPasswordBody(_userEmail, DeviceIdentifier);
        body["SsoEmail2FaSessionToken"] = "";
        body["AuthRequestAccessCode"] = "";
        body["AuthRequestId"] = "";

        var response = await SendEmailLoginAsync(body, headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Login);
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, code));
    }

    /// <summary>
    /// SSO: the server validates the session token from the two-factor challenge, and only a valid token lets it
    /// email the two-factor code. That emailed code logs the device in through the authorization code grant.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_SsoSessionToken_EmailsCodeThatLogsInThroughSso()
    {
        await EnrollUserInEmail();
        var sso = await ArrangeSsoLoginAsync();

        var challenge = await PostSsoTokenAsync(sso, DeviceIdentifier, twoFactorToken: null);
        Assert.Equal("Two factor required.", challenge.GetProperty("error_description").GetString());
        var ssoSessionToken = challenge.GetProperty("SsoEmail2faSessionToken").GetString();

        var response = await SendEmailLoginAsync(
            new Dictionary<string, string?>
            {
                ["Email"] = _userEmail,
                ["SsoEmail2FaSessionToken"] = ssoSessionToken,
            },
            headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Login);
        AssertLoggedIn(await PostSsoTokenAsync(sso, DeviceIdentifier, code));
    }

    /// <summary>
    /// Log in with device: the server validates the approved auth request's access code, and only a valid code
    /// lets it email the two-factor code. That emailed code logs the device in.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_ApprovedAuthRequest_EmailsCodeThatLogsIn()
    {
        await EnrollUserInEmail();
        var authRequest = await CreateApprovedAuthRequestAsync();
        await ChallengeAsync(DeviceIdentifier);

        var response = await SendEmailLoginAsync(
            AuthRequestBody(_userEmail, authRequest.Id, AuthRequestAccessCode),
            headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Login);
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, code));
    }

    /// <summary>
    /// The login code goes to the two-factor email address, which can differ from the account email, and still
    /// logs the device in.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_TwoFactorAddressDiffersFromAccountEmail_EmailsCodeToTwoFactorAddress()
    {
        var twoFactorAddress = $"two-factor-inbox-{Guid.NewGuid()}@bitwarden.com";
        await SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildEmailProvidersJson(twoFactorAddress));
        await ChallengeAsync(DeviceIdentifier);

        var response = await SendEmailLoginAsync(
            MasterPasswordBody(_userEmail), headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var emailed = FindLatestEmailedTwoFactorCode(_mailService, _userEmail);
        Assert.NotNull(emailed);
        Assert.Equal(twoFactorAddress, emailed.Recipient);
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, emailed.Code));
    }

    /// <summary>
    /// A user without email two-factor gets a server error even with the correct master password, and no code
    /// is emailed. The service finds no two-factor email address and throws, and the API does not map that
    /// exception to a client error.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_UserWithoutEmailTwoFactor_ServerErrorAndNoEmail()
    {
        var response = await SendEmailLoginAsync(
            MasterPasswordBody(_userEmail), headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertNoCodeEmailed(_userEmail);
    }

    /// <summary>
    /// A request to email a two-factor code is rejected when it carries no master password, OTP, access code, or
    /// SSO session token, and no code is emailed.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_NoCredentials_BadRequestAndNoEmail()
    {
        await EnrollUserInEmail();

        var response = await SendEmailLoginAsync(
            new Dictionary<string, string?> { ["Email"] = _userEmail }, headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNoCodeEmailed(_userEmail);
    }

    /// <summary>
    /// A wrong master password is rejected, and no code is emailed.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_WrongMasterPassword_BadRequestAndNoEmail()
    {
        await EnrollUserInEmail();

        var response = await SendEmailLoginAsync(
            new Dictionary<string, string?>
            {
                ["Email"] = _userEmail,
                ["MasterPasswordHash"] = "wrong_master_password_hash",
            },
            headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(CannotSendTwoFactorEmailMessage, await response.Content.ReadAsStringAsync());
        AssertNoCodeEmailed(_userEmail);
    }

    /// <summary>
    /// An email with no account gets the same rejection as a wrong password, and no code is emailed.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_UnknownEmail_BadRequestAndNoEmail()
    {
        var unknownEmail = $"unknown-{Guid.NewGuid()}@bitwarden.com";

        var response = await SendEmailLoginAsync(
            MasterPasswordBody(unknownEmail), headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(CannotSendTwoFactorEmailMessage, await response.Content.ReadAsStringAsync());
        AssertNoCodeEmailed(unknownEmail);
    }

    /// <summary>
    /// An SSO session token that cannot be unprotected is rejected with a message naming the token, and no code is
    /// emailed.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_InvalidSsoSessionToken_BadRequestAndNoEmail()
    {
        await EnrollUserInEmail();

        var response = await SendEmailLoginAsync(
            new Dictionary<string, string?>
            {
                ["Email"] = _userEmail,
                ["SsoEmail2FaSessionToken"] = "not-a-session-token",
            },
            headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(InvalidSsoSessionTokenMessage, await response.Content.ReadAsStringAsync());
        AssertNoCodeEmailed(_userEmail);
    }

    /// <summary>
    /// An auth request with the wrong access code gets the same rejection as a wrong password, and no code is
    /// emailed.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_WrongAuthRequestAccessCode_BadRequestAndNoEmail()
    {
        await EnrollUserInEmail();
        var authRequest = await CreateApprovedAuthRequestAsync();

        var response = await SendEmailLoginAsync(
            AuthRequestBody(_userEmail, authRequest.Id, "wrong-access-code"), headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(CannotSendTwoFactorEmailMessage, await response.Content.ReadAsStringAsync());
        AssertNoCodeEmailed(_userEmail);
    }

    // ---------------------------------------------------------------------
    // Emailed login code lifecycle
    // ---------------------------------------------------------------------

    /// <summary>
    /// A login code works once. A second login with the same code is rejected.
    /// </summary>
    [Fact]
    public async Task EmailLoginCode_UsedTwice_SecondLoginRejected()
    {
        await EnrollUserInEmail();
        var code = await ChallengeAndEmailLoginCodeAsync(DeviceIdentifier);
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, code));

        var secondLogin = await LogInWithTwoFactorAsync(DeviceIdentifier, code);

        AssertTwoFactorRejected(secondLogin);
    }

    /// <summary>
    /// Sending a new login code replaces the previous one: only the newer code logs in.
    /// </summary>
    [Fact]
    public async Task EmailLoginCode_NewerCodeSent_OlderCodeRejectedAndNewerCodeLogsIn()
    {
        await EnrollUserInEmail();
        var olderCode = await ChallengeAndEmailLoginCodeAsync(DeviceIdentifier);
        var newerCode = await EmailLoginCodeAsync(DeviceIdentifier);

        // Two random codes can coincide, which would hide whether the older code was replaced, so resend until
        // they differ.
        for (var attempt = 0; attempt < 5 && newerCode == olderCode; attempt++)
        {
            newerCode = await EmailLoginCodeAsync(DeviceIdentifier);
        }

        Assert.NotEqual(olderCode, newerCode);
        AssertTwoFactorRejected(await LogInWithTwoFactorAsync(DeviceIdentifier, olderCode));
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, newerCode));
    }

    /// <summary>
    /// A security stamp change, such as a password change, invalidates a pending login code.
    /// </summary>
    [Fact]
    public async Task EmailLoginCode_SecurityStampChangedBeforeUse_LoginRejected()
    {
        await EnrollUserInEmail();
        var code = await ChallengeAndEmailLoginCodeAsync(DeviceIdentifier);

        var user = await GetUserAsync();
        user.SecurityStamp = Guid.NewGuid().ToString();
        await _userRepository.ReplaceAsync(user);

        AssertTwoFactorRejected(await LogInWithTwoFactorAsync(DeviceIdentifier, code));
    }

    // ---------------------------------------------------------------------
    // Device binding
    // ---------------------------------------------------------------------

    /// <summary>
    /// A login code only works on the device that requested it. A failed attempt from another device does not
    /// use the code up.
    /// </summary>
    [Fact]
    public async Task EmailLoginCode_SubmittedFromOtherDevice_RejectedAndRequestingDeviceLogsIn()
    {
        await EnrollUserInEmail();
        var code = await ChallengeAndEmailLoginCodeAsync(DeviceIdentifier);

        AssertTwoFactorRejected(await LogInWithTwoFactorAsync(OtherDeviceIdentifier, code));
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, code));
    }

    /// <summary>
    /// A request with no device identifier in the header or the body is rejected, and no code is emailed. The
    /// response is the same for a known and an unknown account.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_NoDeviceIdentifier_SameRejectionForKnownAndUnknownAccount()
    {
        await EnrollUserInEmail();
        var unknownEmail = $"unknown-{Guid.NewGuid()}@bitwarden.com";

        var knownAccountResponse = await SendEmailLoginAsync(MasterPasswordBody(_userEmail), headerDeviceIdentifier: null);
        var unknownAccountResponse = await SendEmailLoginAsync(
            MasterPasswordBody(unknownEmail), headerDeviceIdentifier: null);

        Assert.Equal(HttpStatusCode.BadRequest, knownAccountResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownAccountResponse.StatusCode);
        var knownAccountBody = await knownAccountResponse.Content.ReadAsStringAsync();
        Assert.Contains(DeviceIdentifierRequiredMessage, knownAccountBody);
        Assert.Equal(knownAccountBody, await unknownAccountResponse.Content.ReadAsStringAsync());
        AssertNoCodeEmailed(_userEmail);
        AssertNoCodeEmailed(unknownEmail);
    }

    /// <summary>
    /// A device identifier longer than a device record can store is rejected, whether it arrives in the header or
    /// the body, and no code is emailed.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendEmailLogin_OverLongDeviceIdentifier_BadRequestAndNoEmail(bool inHeader)
    {
        await EnrollUserInEmail();
        var overLongDeviceIdentifier = new string('d', Device.MaxIdentifierLength + 1);

        var response = inHeader
            ? await SendEmailLoginAsync(MasterPasswordBody(_userEmail), overLongDeviceIdentifier)
            : await SendEmailLoginAsync(
                MasterPasswordBody(_userEmail, overLongDeviceIdentifier), headerDeviceIdentifier: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(DeviceIdentifierRequiredMessage, await response.Content.ReadAsStringAsync());
        AssertNoCodeEmailed(_userEmail);
    }

    /// <summary>
    /// When the header and the body name different devices, the code is bound to the header's device.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_HeaderAndBodyDevicesDiffer_CodeBoundToHeaderDevice()
    {
        await EnrollUserInEmail();
        await ChallengeAsync(DeviceIdentifier);

        var response = await SendEmailLoginAsync(
            MasterPasswordBody(_userEmail, OtherDeviceIdentifier), headerDeviceIdentifier: DeviceIdentifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Login);
        AssertTwoFactorRejected(await LogInWithTwoFactorAsync(OtherDeviceIdentifier, code));
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, code));
    }

    // TODO: PM-44555 - Delete this test once every supported mobile client version sends the Device-Identifier
    // header on send-email-login and the body fallback is removed.
    /// <summary>
    /// Without a header, the code is bound to the device named in the body.
    /// </summary>
    [Fact]
    public async Task SendEmailLogin_DeviceInBodyOnly_CodeBoundToBodyDevice()
    {
        await EnrollUserInEmail();
        await ChallengeAsync(DeviceIdentifier);

        var response = await SendEmailLoginAsync(
            MasterPasswordBody(_userEmail, DeviceIdentifier), headerDeviceIdentifier: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = AssertCodeEmailed(TwoFactorEmailPurpose.Login);
        AssertTwoFactorRejected(await LogInWithTwoFactorAsync(OtherDeviceIdentifier, code));
        AssertLoggedIn(await LogInWithTwoFactorAsync(DeviceIdentifier, code));
    }

    /// <summary>
    /// A setup code does not work as a login code, even on the device it was issued to.
    /// </summary>
    [Fact]
    public async Task EmailSetupCode_SubmittedAtLogin_Rejected()
    {
        await EnrollUserInEmail();
        var uvToken = await GetEmailUserVerificationTokenAsync();
        var setupCode = await EmailSetupCodeAsync(_client, _userEmail, uvToken);

        var result = await LogInWithTwoFactorAsync(IdentityApplicationFactory.DefaultDeviceIdentifier, setupCode);

        AssertTwoFactorRejected(result);
    }

    /// <summary>
    /// A login code does not work as a setup code, even from the device it was issued to.
    /// </summary>
    [Fact]
    public async Task EmailLoginCode_SubmittedToPutEmail_BadRequest()
    {
        await EnrollUserInEmail();
        var loginCode = await ChallengeAndEmailLoginCodeAsync(IdentityApplicationFactory.DefaultDeviceIdentifier);
        var uvToken = await GetEmailUserVerificationTokenAsync();

        var response = await _client.PutAsJsonAsync("/two-factor/email",
            new TwoFactorEmailUpdateRequestModel { Email = _userEmail, Token = loginCode, UserVerificationToken = uvToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid token.", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// A setup code only works from a session on the device that requested it: a session on another device is
    /// rejected, and the requesting device's session still succeeds.
    /// </summary>
    [Fact]
    public async Task PutEmail_SetupCodeFromOtherDeviceSession_RejectedAndRequestingDeviceSucceeds()
    {
        var uvToken = await GetEmailUserVerificationTokenAsync();
        var setupCode = await EmailSetupCodeAsync(_client, _userEmail, uvToken);
        using var otherDeviceClient = await CreateClientForDeviceAsync(OtherDeviceIdentifier);
        var model = new TwoFactorEmailUpdateRequestModel
        {
            Email = _userEmail,
            Token = setupCode,
            UserVerificationToken = uvToken,
        };

        var otherDeviceResponse = await otherDeviceClient.PutAsJsonAsync("/two-factor/email", model);
        var requestingDeviceResponse = await _client.PutAsJsonAsync("/two-factor/email", model);

        Assert.Equal(HttpStatusCode.BadRequest, otherDeviceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, requestingDeviceResponse.StatusCode);
    }

    /// <summary>
    /// A setup code is bound to the session's device even when the request also sends a <c>Device-Identifier</c>
    /// header naming another device: a session on the named device is rejected, and the requesting session succeeds.
    /// </summary>
    [Fact]
    public async Task SendEmailSetup_DeviceHeaderNamesOtherDevice_CodeBoundToSessionDevice()
    {
        var uvToken = await GetEmailUserVerificationTokenAsync();
        using var sendRequest = new HttpRequestMessage(HttpMethod.Post, "/two-factor/send-email");
        sendRequest.Headers.Add("Device-Identifier", OtherDeviceIdentifier);
        sendRequest.Content = JsonContent.Create(new { Email = _userEmail, UserVerificationToken = uvToken });
        var sendResponse = await _client.SendAsync(sendRequest);
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var setupCode = AssertCodeEmailed(TwoFactorEmailPurpose.Setup);
        using var otherDeviceClient = await CreateClientForDeviceAsync(OtherDeviceIdentifier);
        var model = new TwoFactorEmailUpdateRequestModel
        {
            Email = _userEmail,
            Token = setupCode,
            UserVerificationToken = uvToken,
        };

        var otherDeviceResponse = await otherDeviceClient.PutAsJsonAsync("/two-factor/email", model);
        var requestingDeviceResponse = await _client.PutAsJsonAsync("/two-factor/email", model);

        Assert.Equal(HttpStatusCode.BadRequest, otherDeviceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, requestingDeviceResponse.StatusCode);
    }

    /// <summary>
    /// Refreshing the access token between requesting and submitting a setup code keeps the session's device, so
    /// the code still works.
    /// </summary>
    [Fact]
    public async Task PutEmail_AccessTokenRefreshedAfterSendEmail_EnablesProvider()
    {
        var (accessToken, refreshToken) = await _factory.Identity.TokenFromPasswordAsync(_userEmail, MasterPasswordHash);
        using var client = CreateAuthenticatedClient(accessToken);
        var uvToken = await GetEmailUserVerificationTokenAsync(client);
        var setupCode = await EmailSetupCodeAsync(client, _userEmail, uvToken);

        using var refreshedClient = CreateAuthenticatedClient(await RefreshAccessTokenAsync(refreshToken));
        var response = await refreshedClient.PutAsJsonAsync("/two-factor/email",
            new TwoFactorEmailUpdateRequestModel { Email = _userEmail, Token = setupCode, UserVerificationToken = uvToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull((await GetUserAsync()).GetTwoFactorProvider(TwoFactorProviderType.Email));
    }

    /// <summary>
    /// A login code stored in the previous cache format no longer logs in. A user holding one requests a new code.
    /// </summary>
    [Fact]
    public async Task EmailLoginCode_StoredInPreviousFormat_Rejected()
    {
        await EnrollUserInEmail();
        await ChallengeAsync(DeviceIdentifier);
        var user = await GetUserAsync();
        const string previousFormatCode = "123456";
        var cache = _factory.Identity.Services.GetRequiredKeyedService<IDistributedCache>("persistent");
        await cache.SetStringAsync($"EmailToken_{user.Id}_{user.SecurityStamp}_TwoFactor", previousFormatCode);

        var result = await LogInWithTwoFactorAsync(DeviceIdentifier, previousFormatCode);

        AssertTwoFactorRejected(result);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private sealed record SsoLogin(string AuthorizationCode, string CodeVerifier);

    private Task EnrollUserInEmail() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildEmailProvidersJson(_userEmail));

    private async Task<User> GetUserAsync()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(user);
        return user;
    }

    private Task<string> GetEmailUserVerificationTokenAsync() => GetEmailUserVerificationTokenAsync(_client);

    private static async Task<string> GetEmailUserVerificationTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/two-factor/get-email",
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(response, "email");
        return uvToken;
    }

    /// <summary>Requests a setup code for the given address and returns the code that was emailed.</summary>
    private async Task<string> EmailSetupCodeAsync(HttpClient client, string twoFactorAddress, string uvToken)
    {
        var response = await client.PostAsJsonAsync("/two-factor/send-email",
            new { Email = twoFactorAddress, UserVerificationToken = uvToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return AssertCodeEmailed(TwoFactorEmailPurpose.Setup);
    }

    /// <summary>Logs the test user in on the given device and returns an API client for that session.</summary>
    private async Task<HttpClient> CreateClientForDeviceAsync(string deviceIdentifier)
    {
        var (accessToken, _) = await _factory.Identity.TokenFromPasswordAsync(
            _userEmail, MasterPasswordHash, deviceIdentifier);
        return CreateAuthenticatedClient(accessToken);
    }

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<string> RefreshAccessTokenAsync(string refreshToken)
    {
        var context = await _factory.Identity.Server.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", "web" },
                { "refresh_token", refreshToken },
            }));
        var root = await ReadIdentityJsonAsync(context);
        AssertLoggedIn(root);
        return root.GetProperty("access_token").GetString()!;
    }

    /// <summary>Requests a token without a two-factor code and asserts the two-factor challenge comes back.</summary>
    private async Task ChallengeAsync(string deviceIdentifier)
    {
        var context = await _factory.Identity.ContextFromPasswordAsync(
            _userEmail, MasterPasswordHash, deviceIdentifier);
        var root = await ReadIdentityJsonAsync(context);
        Assert.True(root.TryGetProperty("error_description", out var errorDescription), root.ToString());
        Assert.Equal("Two factor required.", errorDescription.GetString());
    }

    private async Task<string> ChallengeAndEmailLoginCodeAsync(string deviceIdentifier)
    {
        await ChallengeAsync(deviceIdentifier);
        return await EmailLoginCodeAsync(deviceIdentifier);
    }

    /// <summary>Requests a login code with the master password and returns the code that was emailed.</summary>
    private async Task<string> EmailLoginCodeAsync(string deviceIdentifier)
    {
        var response = await SendEmailLoginAsync(
            MasterPasswordBody(_userEmail), headerDeviceIdentifier: deviceIdentifier);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return AssertCodeEmailed(TwoFactorEmailPurpose.Login);
    }

    private async Task<JsonElement> LogInWithTwoFactorAsync(string deviceIdentifier, string code)
    {
        var context = await _factory.Identity.ContextFromPasswordWithTwoFactorAsync(
            _userEmail,
            MasterPasswordHash,
            deviceIdentifier,
            twoFactorProviderType: ((int)TwoFactorProviderType.Email).ToString(CultureInfo.InvariantCulture),
            twoFactorToken: code);
        return await ReadIdentityJsonAsync(context);
    }

    private static async Task<JsonElement> ReadIdentityJsonAsync(HttpContext context)
    {
        using var document = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.NotNull(document);
        return document.RootElement.Clone();
    }

    private static void AssertLoggedIn(JsonElement tokenResponse)
    {
        Assert.True(tokenResponse.TryGetProperty("access_token", out var accessToken), tokenResponse.ToString());
        Assert.False(string.IsNullOrWhiteSpace(accessToken.GetString()));
    }

    private static void AssertTwoFactorRejected(JsonElement tokenResponse)
    {
        Assert.False(tokenResponse.TryGetProperty("access_token", out _), tokenResponse.ToString());
        Assert.Equal(InvalidTwoFactorTokenMessage,
            tokenResponse.GetProperty("ErrorModel").GetProperty("Message").GetString());
    }

    private static Dictionary<string, string?> MasterPasswordBody(string email, string? bodyDeviceIdentifier = null)
    {
        var body = new Dictionary<string, string?>
        {
            ["Email"] = email,
            ["MasterPasswordHash"] = MasterPasswordHash,
        };
        if (bodyDeviceIdentifier != null)
        {
            body["DeviceIdentifier"] = bodyDeviceIdentifier;
        }

        return body;
    }

    private static Dictionary<string, string?> AuthRequestBody(string email, Guid authRequestId, string accessCode) =>
        new()
        {
            ["Email"] = email,
            ["AuthRequestId"] = authRequestId.ToString(),
            ["AuthRequestAccessCode"] = accessCode,
        };

    /// <summary>Calls send-email-login without a bearer token, as a client does in the middle of login.</summary>
    private async Task<HttpResponseMessage> SendEmailLoginAsync(
        Dictionary<string, string?> body, string? headerDeviceIdentifier)
    {
        using var client = _factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/two-factor/send-email-login");
        if (headerDeviceIdentifier != null)
        {
            message.Headers.Add("Device-Identifier", headerDeviceIdentifier);
        }

        message.Content = JsonContent.Create(body);
        return await client.SendAsync(message);
    }

    /// <summary>
    /// Asserts a code was emailed for the given purpose and that it has the six-digit numeric shape clients
    /// accept, then returns it.
    /// </summary>
    private string AssertCodeEmailed(TwoFactorEmailPurpose purpose)
    {
        var emailed = FindLatestEmailedTwoFactorCode(_mailService, _userEmail);
        Assert.NotNull(emailed);
        Assert.Equal(purpose, emailed.Purpose);
        Assert.Matches("^[0-9]{6}$", emailed.Code);
        return emailed.Code;
    }

    private void AssertNoCodeEmailed(string accountEmail) =>
        Assert.Null(FindLatestEmailedTwoFactorCode(_mailService, accountEmail));

    /// <summary>Returns a code of the same shape that is guaranteed not to match.</summary>
    private static string WrongCodeFor(string code) => code == "000000" ? "111111" : "000000";

    private async Task<AuthRequest> CreateApprovedAuthRequestAsync()
    {
        var user = await GetUserAsync();
        return await _factory.GetService<IAuthRequestRepository>().CreateAsync(new AuthRequest
        {
            UserId = user.Id,
            Type = AuthRequestType.AuthenticateAndUnlock,
            RequestDeviceIdentifier = DeviceIdentifier,
            RequestDeviceType = DeviceType.FirefoxBrowser,
            RequestIpAddress = FactoryConstants.WhitelistedIp,
            AccessCode = AuthRequestAccessCode,
            PublicKey = "public-key",
            Key = "key",
            Approved = true,
            ResponseDate = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Puts the user in an organization that uses SSO and registers an authorization code that signs the user in
    /// through it.
    /// </summary>
    private async Task<SsoLogin> ArrangeSsoLoginAsync()
    {
        var user = await GetUserAsync();

        var organization = await _factory.GetService<IOrganizationRepository>().CreateAsync(new Organization
        {
            Name = "Two Factor Email SSO Org",
            BillingEmail = "billing-email@example.com",
            Plan = "Enterprise",
            UsePolicies = true,
            UseSso = true,
            Use2fa = true,
        });

        await _factory.GetService<IOrganizationUserRepository>().CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        await _factory.GetService<ISsoConfigRepository>().CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(
                new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.MasterPassword },
                JsonHelpers.CamelCase),
        });

        var codeVerifier = new string('c', 50);
        var authorizationCodeKey = $"sso-code-{Guid.NewGuid()}";
        _ssoAuthorizationCodes[authorizationCodeKey] = new AuthorizationCode
        {
            ClientId = "web",
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = SsoRedirectUri,
            RequestedScopes = ["api", "offline_access"],
            CodeChallenge = codeVerifier.Sha256(),
            CodeChallengeMethod = "plain",
            Subject = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(JwtClaimTypes.Subject, user.Id.ToString()),
                new Claim(JwtClaimTypes.Name, _userEmail),
                new Claim(JwtClaimTypes.IdentityProvider, "sso"),
                new Claim("organizationId", organization.Id.ToString()),
                new Claim(JwtClaimTypes.SessionId, "SOMETHING"),
                new Claim(JwtClaimTypes.AuthenticationMethod, "external"),
                new Claim(JwtClaimTypes.AuthenticationTime,
                    new DateTimeOffset(DateTime.UtcNow.AddMinutes(-1)).ToUnixTimeSeconds()
                        .ToString(CultureInfo.InvariantCulture))
            ], "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role)),
        };

        return new SsoLogin(authorizationCodeKey, codeVerifier);
    }

    private async Task<JsonElement> PostSsoTokenAsync(SsoLogin sso, string deviceIdentifier, string? twoFactorToken)
    {
        var form = new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", ((int)DeviceType.FirefoxBrowser).ToString(CultureInfo.InvariantCulture) },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", "firefox" },
            { "grant_type", "authorization_code" },
            { "code", sso.AuthorizationCode },
            { "code_verifier", sso.CodeVerifier },
            { "redirect_uri", SsoRedirectUri },
        };
        if (twoFactorToken != null)
        {
            form["twoFactorToken"] = twoFactorToken;
            form["twoFactorProvider"] = ((int)TwoFactorProviderType.Email).ToString(CultureInfo.InvariantCulture);
            form["twoFactorRemember"] = "0";
        }

        var context = await _factory.Identity.Server.PostAsync("/connect/token", new FormUrlEncodedContent(form));
        return await ReadIdentityJsonAsync(context);
    }
}
