#nullable enable

using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Controllers;

/// <summary>
/// Covers the request-otp and verify-otp endpoints, and the api-key endpoint as a representative of the
/// endpoints that accept an emailed OTP as a user verification secret. The tests run the real user service,
/// token provider, and distributed cache. Only the mail service is substituted, so tests can read the OTP it
/// would have emailed.
/// </summary>
public class AccountsControllerOtpTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IMailService _mailService;

    private const string OwnerMasterPasswordHash = "master_password_hash";
    private const int FailedAttemptLimit = 5;

    private string _ownerEmail = null!;

    public AccountsControllerOtpTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IMailService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _mailService = _factory.GetService<IMailService>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail, OwnerMasterPasswordHash);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PostRequestOtp_Authenticated_OkAndEmailsOtpToUser()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await PostRequestOtpAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _mailService.Received(1).SendOTPEmailAsync(_ownerEmail, Arg.Any<string>());
    }

    [Fact]
    public async Task PostRequestOtp_Unauthorized_ReturnsUnauthorized()
    {
        // Don't call LoginAsync to test unauthorized access

        var response = await PostRequestOtpAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await _mailService.DidNotReceive().SendOTPEmailAsync(_ownerEmail, Arg.Any<string>());
    }

    [Fact]
    public async Task PostVerifyOtp_ValidOtp_Ok()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostVerifyOtpAsync(otp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostVerifyOtp_WrongOtp_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostVerifyOtpAsync(WrongOtpFor(otp));

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task PostVerifyOtp_OtpAlreadyUsed_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        var firstResponse = await PostVerifyOtpAsync(otp);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await PostVerifyOtpAsync(otp);

        await AssertInvalidTokenAsync(secondResponse);
    }

    /// <summary>
    /// Expiry is simulated by evicting the OTP's cache entry, because the test host cannot advance the clock.
    /// A real timeout removes the same entry.
    /// </summary>
    [Fact]
    public async Task PostVerifyOtp_OtpExpired_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        await ExpireOtpAsync(_ownerEmail);

        var response = await PostVerifyOtpAsync(otp);

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task PostVerifyOtp_ValidOtp_EmailNotVerified_SetsEmailVerified()
    {
        await SetEmailVerifiedAsync(_ownerEmail, false);
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostVerifyOtpAsync(otp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        Assert.True(user.EmailVerified);
    }

    [Fact]
    public async Task PostVerifyOtp_WrongOtp_EmailNotVerified_LeavesEmailUnverified()
    {
        await SetEmailVerifiedAsync(_ownerEmail, false);
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostVerifyOtpAsync(WrongOtpFor(otp));

        await AssertInvalidTokenAsync(response);
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        Assert.False(user.EmailVerified);
    }

    [Fact]
    public async Task PostVerifyOtp_ValidOtp_EmailAlreadyVerified_DoesNotUpdateUser()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var userBefore = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userBefore);
        Assert.True(userBefore.EmailVerified);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostVerifyOtpAsync(otp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var userAfter = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userAfter);
        Assert.True(userAfter.EmailVerified);
        Assert.Equal(userBefore.RevisionDate, userAfter.RevisionDate);
    }

    [Fact]
    public async Task PostVerifyOtp_Unauthorized_ReturnsUnauthorized()
    {
        // Don't call LoginAsync to test unauthorized access

        var response = await PostVerifyOtpAsync("12345678");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostRequestOtp_CalledTwice_FirstOtpRejected()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var firstOtp = await RequestOtpAndCaptureAsync();
        var secondOtp = await RequestOtpAndCaptureAsync();
        Assert.NotEqual(firstOtp, secondOtp);

        var firstResponse = await PostVerifyOtpAsync(firstOtp);
        var secondResponse = await PostVerifyOtpAsync(secondOtp);

        await AssertInvalidTokenAsync(firstResponse);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    [Fact]
    public async Task PostVerifyOtp_EmptyOtp_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        await RequestOtpAndCaptureAsync();

        var response = await PostVerifyOtpAsync("");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostVerifyOtp_SecurityStampChangedAfterRequest_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        await UpdateUserAsync(_ownerEmail, user => user.SecurityStamp = Guid.NewGuid().ToString());

        var response = await PostVerifyOtpAsync(otp);

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task PostVerifyOtp_EmailChangedAfterRequest_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        var newEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await UpdateUserAsync(_ownerEmail, user => user.Email = newEmail);

        var response = await PostVerifyOtpAsync(otp);

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task PostApiKey_ValidOtp_Ok()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostApiKeyAsync(otp: otp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostApiKey_WrongOtp_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostApiKeyAsync(otp: WrongOtpFor(otp));

        await AssertUserVerificationFailedAsync(response);
    }

    [Fact]
    public async Task PostApiKey_MasterPasswordHash_Ok()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await PostApiKeyAsync(masterPasswordHash: OwnerMasterPasswordHash);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostApiKey_NoMasterPassword_ValidOtp_Ok()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        await UpdateUserAsync(_ownerEmail, user => user.MasterPassword = null);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostApiKeyAsync(otp: otp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostApiKey_NoMasterPassword_WrongOtp_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        await UpdateUserAsync(_ownerEmail, user => user.MasterPassword = null);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostApiKeyAsync(otp: WrongOtpFor(otp));

        await AssertUserVerificationFailedAsync(response);
    }

    [Fact]
    public async Task PostApiKey_OtpAlreadyUsedOnVerifyOtp_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        var verifyResponse = await PostVerifyOtpAsync(otp);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var response = await PostApiKeyAsync(otp: otp);

        await AssertUserVerificationFailedAsync(response);
    }

    [Fact]
    public async Task PostVerifyOtp_OtpAlreadyUsedOnApiKey_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        var apiKeyResponse = await PostApiKeyAsync(otp: otp);
        Assert.Equal(HttpStatusCode.OK, apiKeyResponse.StatusCode);

        var response = await PostVerifyOtpAsync(otp);

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task PostApiKey_ValidOtp_EmailNotVerified_LeavesEmailUnverified()
    {
        await SetEmailVerifiedAsync(_ownerEmail, false);
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();

        var response = await PostApiKeyAsync(otp: otp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        Assert.False(user.EmailVerified);
    }

    [Fact]
    public async Task PostVerifyOtp_OneFewerFailedAttemptsThanLimit_ValidOtpOk()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        for (var i = 0; i < FailedAttemptLimit - 1; i++)
        {
            await AssertInvalidTokenAsync(await PostVerifyOtpAsync(WrongOtpFor(otp)));
        }

        var response = await PostVerifyOtpAsync(otp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostVerifyOtp_FailedAttemptLimitReached_ValidOtpRejected()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        for (var i = 0; i < FailedAttemptLimit; i++)
        {
            await AssertInvalidTokenAsync(await PostVerifyOtpAsync(WrongOtpFor(otp)));
        }

        var response = await PostVerifyOtpAsync(otp);

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task PostVerifyOtp_FailedAttemptLimitReached_NewOtpOk()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var firstOtp = await RequestOtpAndCaptureAsync();
        for (var i = 0; i < FailedAttemptLimit; i++)
        {
            await AssertInvalidTokenAsync(await PostVerifyOtpAsync(WrongOtpFor(firstOtp)));
        }
        var secondOtp = await RequestOtpAndCaptureAsync();

        var response = await PostVerifyOtpAsync(secondOtp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Reaching the failed-attempt limit on verify-otp removes the OTP, so an endpoint that accepts a user
    /// verification secret also rejects it.
    /// </summary>
    [Fact]
    public async Task PostApiKey_FailedAttemptLimitReachedOnVerifyOtp_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        for (var i = 0; i < FailedAttemptLimit; i++)
        {
            await AssertInvalidTokenAsync(await PostVerifyOtpAsync(WrongOtpFor(otp)));
        }

        var response = await PostApiKeyAsync(otp: otp);

        await AssertUserVerificationFailedAsync(response);
    }

    /// <summary>
    /// Endpoints that accept a user verification secret validate the OTP without counting failed attempts, so
    /// wrong OTPs there do not use up the verify-otp limit.
    /// </summary>
    [Fact]
    public async Task PostVerifyOtp_WrongOtpsOnApiKey_DoNotCountTowardFailedAttemptLimit()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var otp = await RequestOtpAndCaptureAsync();
        for (var i = 0; i < FailedAttemptLimit; i++)
        {
            await AssertUserVerificationFailedAsync(await PostApiKeyAsync(otp: WrongOtpFor(otp)));
        }

        var response = await PostVerifyOtpAsync(otp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostRequestOtpAsync()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/request-otp");
        return await _client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> PostVerifyOtpAsync(string otp)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/verify-otp");
        message.Content = JsonContent.Create(new VerifyOTPRequestModel { OTP = otp });
        return await _client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> PostApiKeyAsync(string? otp = null, string? masterPasswordHash = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/api-key");
        message.Content = JsonContent.Create(
            new SecretVerificationRequestModel { OTP = otp, MasterPasswordHash = masterPasswordHash });
        return await _client.SendAsync(message);
    }

    /// <summary>
    /// Calls request-otp as the logged-in owner and returns the OTP handed to the substituted mail service.
    /// </summary>
    private async Task<string> RequestOtpAndCaptureAsync()
    {
        string? otp = null;
        _mailService
            .When(m => m.SendOTPEmailAsync(_ownerEmail, Arg.Any<string>()))
            .Do(call => otp = call.ArgAt<string>(1));

        var response = await PostRequestOtpAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(otp);
        return otp;
    }

    /// <summary>
    /// Returns a code of the same length that differs from the given OTP at every position.
    /// </summary>
    private static string WrongOtpFor(string otp) =>
        new(otp.Select(c => c == '0' ? '1' : '0').ToArray());

    private Task SetEmailVerifiedAsync(string email, bool emailVerified) =>
        UpdateUserAsync(email, user => user.EmailVerified = emailVerified);

    private async Task UpdateUserAsync(string email, Action<User> update)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        Assert.NotNull(user);
        update(user);
        await _userRepository.ReplaceAsync(user);
    }

    private async Task ExpireOtpAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        Assert.NotNull(user);

        var cache = _factory.Services.GetRequiredKeyedService<IDistributedCache>("persistent");
        var otpCacheKey = OtpCacheKey(user);

        Assert.NotNull(await cache.GetAsync(otpCacheKey));
        await cache.RemoveAsync(otpCacheKey);
    }

    private static string OtpCacheKey(User user) =>
        $"UserVerification_EmailOtp_{user.Id}_{user.SecurityStamp}";

    private static async Task AssertInvalidTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid token", content);
    }

    private static async Task AssertUserVerificationFailedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("User verification failed.", content);
    }
}
