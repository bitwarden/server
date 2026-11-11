using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Auth.Helpers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.IntegrationTestCommon.Factories;
using Core.Auth.Enums;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorEmailAssertions;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorEmailTestHelpers;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Auth.TwoFactor;

/// <summary>
/// Covers turning email two-factor on and off with the real two-factor email service: get-email, delete,
/// send-email, and PUT email, including the binding of a setup code to the session's device.
/// Only the mail service is substituted, so tests read the emailed code from it. The API host shares the
/// Identity host's persistent cache, so a login code emailed by the API can be redeemed against the Identity
/// token endpoint, as it is in a deployed environment.
/// </summary>
public class TwoFactorEmailSetupTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string DeviceIdentifier = "two-factor-email-device";
    private const string OtherDeviceIdentifier = "two-factor-email-other-device";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> _userVerificationTokenFactory;

    private string _userEmail = null!;

    public TwoFactorEmailSetupTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        SubstituteTwoFactorProviderServices(_factory);
        _factory.SubstituteService<IMailService>(_ => { });
        _factory.ShareIdentityPersistentCache();
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
    // Device binding
    // ---------------------------------------------------------------------

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
    /// Full setup on an SSO session whose access token has been refreshed. Clients refresh the access token right
    /// after an SSO login, so the refreshed token must keep the session's device for setup to work.
    /// </summary>
    [Fact]
    public async Task PutEmail_SsoAccessTokenRefreshed_EnablesProvider()
    {
        var sso = await ArrangeSsoLoginAsync();
        var login = await PostSsoTokenAsync(sso, DeviceIdentifier, twoFactorToken: null);
        AssertLoggedIn(login);
        var refreshToken = login.GetProperty("refresh_token").GetString()!;
        using var client = CreateAuthenticatedClient(await RefreshAccessTokenAsync(refreshToken));
        var uvToken = await GetEmailUserVerificationTokenAsync(client);
        var setupCode = await EmailSetupCodeAsync(client, _userEmail, uvToken);

        var response = await client.PutAsJsonAsync("/two-factor/email",
            new TwoFactorEmailUpdateRequestModel { Email = _userEmail, Token = setupCode, UserVerificationToken = uvToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var provider = (await GetUserAsync()).GetTwoFactorProvider(TwoFactorProviderType.Email);
        Assert.NotNull(provider);
        Assert.True(provider.Enabled);
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

    private Task<string> GetEmailUserVerificationTokenAsync() => GetEmailUserVerificationTokenAsync(_client);

    private static Task<string> GetEmailUserVerificationTokenAsync(HttpClient client) =>
        TwoFactorEmailTestHelpers.GetEmailUserVerificationTokenAsync(client);

    private Task<string> EmailSetupCodeAsync(HttpClient client, string twoFactorAddress, string uvToken) =>
        TwoFactorEmailTestHelpers.EmailSetupCodeAsync(client, _mailService, _userEmail, twoFactorAddress, uvToken);

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
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "web" },
            { "refresh_token", refreshToken },
        });
        var context = await _factory.Identity.Server.PostAsync("/connect/token", content);
        var root = await ReadIdentityJsonAsync(context);
        AssertLoggedIn(root);
        return root.GetProperty("access_token").GetString()!;
    }

    private string AssertCodeEmailed(TwoFactorEmailPurpose purpose) =>
        TwoFactorEmailAssertions.AssertCodeEmailed(_mailService, _userEmail, purpose);

    /// <summary>Returns a code of the same shape that is guaranteed not to match.</summary>
    private static string WrongCodeFor(string code) => code == "000000" ? "111111" : "000000";

    private async Task<SsoLogin> ArrangeSsoLoginAsync() =>
        await TwoFactorEmailTestHelpers.ArrangeSsoLoginAsync(_factory, await GetUserAsync(), _userEmail);

    private Task<JsonElement> PostSsoTokenAsync(SsoLogin sso, string deviceIdentifier, string? twoFactorToken) =>
        TwoFactorEmailTestHelpers.PostSsoTokenAsync(_factory, sso, deviceIdentifier, twoFactorToken);
}
