using System.Net;
using System.Text.Json;
using Bit.Api.Auth.Models.Request;
using Bit.Api.IntegrationTest.Auth.Helpers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.IntegrationTestCommon.Factories;
using Core.Auth.Enums;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorEmailAssertions;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorEmailTestHelpers;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Auth.TwoFactor;

/// <summary>
/// Covers what an emailed two-factor code can be used for after it is sent, with the real two-factor email
/// service: single use, replacement by a newer code, security stamp changes, device binding, and the
/// separation of login codes from setup codes.
/// Only the mail service is substituted, so tests read the emailed code from it. The API host shares the
/// Identity host's persistent cache, so a login code emailed by the API can be redeemed against the Identity
/// token endpoint, as it is in a deployed environment.
/// </summary>
public class TwoFactorControllerEmailLoginCodeLifecycleTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string DeviceIdentifier = "two-factor-email-device";
    private const string OtherDeviceIdentifier = "two-factor-email-other-device";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;

    private string _userEmail = null!;

    public TwoFactorControllerEmailLoginCodeLifecycleTests(ApiApplicationFactory factory)
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

    private Task EnrollUserInEmail() =>
        SetUserTwoFactorProvidersJsonAsync(
            _userRepository, _userEmail, BuildEmailProvidersJson(_userEmail));

    private async Task<User> GetUserAsync()
    {
        var user = await _userRepository.GetByEmailAsync(_userEmail);
        Assert.NotNull(user);
        return user;
    }

    private Task<string> GetEmailUserVerificationTokenAsync() =>
        TwoFactorEmailTestHelpers.GetEmailUserVerificationTokenAsync(_client);

    private Task<string> EmailSetupCodeAsync(HttpClient client, string twoFactorAddress, string uvToken) =>
        TwoFactorEmailTestHelpers.EmailSetupCodeAsync(client, _mailService, _userEmail, twoFactorAddress, uvToken);

    private Task ChallengeAsync(string deviceIdentifier) =>
        TwoFactorEmailTestHelpers.ChallengeAsync(_factory, _userEmail, deviceIdentifier);

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

    private Task<JsonElement> LogInWithTwoFactorAsync(string deviceIdentifier, string code) =>
        TwoFactorEmailTestHelpers.LogInWithTwoFactorAsync(_factory, _userEmail, deviceIdentifier, code);

    private Task<HttpResponseMessage> SendEmailLoginAsync(
        Dictionary<string, string?> body, string? headerDeviceIdentifier) =>
        TwoFactorEmailTestHelpers.SendEmailLoginAsync(_factory, body, headerDeviceIdentifier);

    private string AssertCodeEmailed(TwoFactorEmailPurpose purpose) =>
        TwoFactorEmailAssertions.AssertCodeEmailed(_mailService, _userEmail, purpose);
}
