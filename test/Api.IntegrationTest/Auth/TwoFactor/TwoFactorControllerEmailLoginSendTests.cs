using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Auth.Helpers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.IntegrationTestCommon.Factories;
using Core.Auth.Enums;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorEmailAssertions;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorEmailTestHelpers;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Auth.TwoFactor;

/// <summary>
/// Covers send-email-login, which emails a two-factor login code during login, with the real two-factor email
/// service: each credential the request can carry, and how the request names its device.
/// Only the mail service is substituted, so tests read the emailed code from it. The API host shares the
/// Identity host's persistent cache, so a login code emailed by the API can be redeemed against the Identity
/// token endpoint, as it is in a deployed environment.
/// </summary>
/// <remarks>
/// Login requests identify their device the way shipped clients do: the <c>Device-Identifier</c> header, a
/// <c>DeviceIdentifier</c> body field, or both.
/// </remarks>
public class TwoFactorControllerEmailLoginSendTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string DeviceIdentifier = "two-factor-email-device";
    private const string OtherDeviceIdentifier = "two-factor-email-other-device";
    private const string AuthRequestAccessCode = "auth-request-access-code";
    private const string CannotSendTwoFactorEmailMessage = "Cannot send two-factor email.";
    private const string DeviceIdentifierRequiredMessage = "A valid device identifier is required.";
    private const string InvalidSsoSessionTokenMessage =
        "a valid, non-expired SSO Email 2FA Session token is required to send 2FA emails.";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;

    private string _userEmail = null!;

    public TwoFactorControllerEmailLoginSendTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        SubstituteTwoFactorProviderServices(_factory);
        _factory.SubstituteService<IMailService>(_ => { });
        _factory.ShareIdentityPersistentCache();
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _mailService = _factory.GetService<IMailService>();
        _userRepository = _factory.GetService<IUserRepository>();
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
    // Device binding
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private Task EnrollUserInEmail() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildEmailProvidersJson(_userEmail));

    private async Task<User> GetUserAsync()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(user);
        return user;
    }

    private Task ChallengeAsync(string deviceIdentifier) =>
        TwoFactorEmailTestHelpers.ChallengeAsync(_factory, _userEmail, deviceIdentifier);

    private Task<JsonElement> LogInWithTwoFactorAsync(string deviceIdentifier, string code) =>
        TwoFactorEmailTestHelpers.LogInWithTwoFactorAsync(_factory, _userEmail, deviceIdentifier, code);

    private static Dictionary<string, string?> AuthRequestBody(string email, Guid authRequestId, string accessCode) =>
        new()
        {
            ["Email"] = email,
            ["AuthRequestId"] = authRequestId.ToString(),
            ["AuthRequestAccessCode"] = accessCode,
        };

    private Task<HttpResponseMessage> SendEmailLoginAsync(
        Dictionary<string, string?> body, string? headerDeviceIdentifier) =>
        TwoFactorEmailTestHelpers.SendEmailLoginAsync(_factory, body, headerDeviceIdentifier);

    private string AssertCodeEmailed(TwoFactorEmailPurpose purpose) =>
        TwoFactorEmailAssertions.AssertCodeEmailed(_mailService, _userEmail, purpose);

    private void AssertNoCodeEmailed(string accountEmail) =>
        Assert.Null(FindLatestEmailedTwoFactorCode(_mailService, accountEmail));

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

    private async Task<SsoLogin> ArrangeSsoLoginAsync() =>
        await TwoFactorEmailTestHelpers.ArrangeSsoLoginAsync(_factory, await GetUserAsync(), _userEmail);

    private Task<JsonElement> PostSsoTokenAsync(SsoLogin sso, string deviceIdentifier, string? twoFactorToken) =>
        TwoFactorEmailTestHelpers.PostSsoTokenAsync(_factory, sso, deviceIdentifier, twoFactorToken);
}
