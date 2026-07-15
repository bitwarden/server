using System.Net;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Controllers.TwoFactor.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Controllers.TwoFactor;

public class TwoFactorControllerEmailTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
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

    [Fact]
    public async Task DeleteEmail_CrossProviderToken_BadRequest()
    {
        var user = (await _userRepository.GetByEmailAsync(_userEmail))!;
        var duoToken = ProtectUserVerificationToken(_userVerificationTokenFactory, user, TwoFactorProviderType.Duo);

        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = duoToken });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User verification failed.", await response.Content.ReadAsStringAsync());
    }

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

    [Fact]
    public async Task DeleteEmail_TamperedToken_BadRequest()
    {
        var response = await SendJsonAsync(_client, HttpMethod.Delete, "/two-factor/email",
            new TwoFactorEmailDeleteRequestModel { UserVerificationToken = "not-a-real-token" });

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
            new { MasterPasswordHash = MasterPasswordHash });
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var (_, uvToken) = await ReadEnabledAndUserVerificationTokenAsync(getResponse, "email");

        // Received-call bookkeeping is per-substitute and IClassFixture keeps the same substitute
        // for the whole test class, so clear here to isolate this test's ordering assertion.
        var emailService = _factory.GetService<ITwoFactorEmailService>();
        emailService.ClearReceivedCalls();

        var sendResponse = await _client.PostAsJsonAsync("/two-factor/send-email",
            new
            {
                Email = _userEmail,
                UserVerificationToken = uvToken,
            });
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        // Ordering: send-email fires SendTwoFactorSetupEmailAsync before PUT is exercised.
        await emailService.Received(1).SendTwoFactorSetupEmailAsync(Arg.Any<User>());

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
            new { MasterPasswordHash = MasterPasswordHash });
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
            new { MasterPasswordHash = MasterPasswordHash });
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
            new { MasterPasswordHash = MasterPasswordHash });
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
                MasterPasswordHash = MasterPasswordHash,
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

    private Task EnrollUserInEmail() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildEmailProvidersJson(_userEmail));
}
