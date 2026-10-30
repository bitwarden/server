#nullable enable

using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Controllers;

/// <summary>
/// Covers the request-otp and verify-otp endpoints, running the real user service, token provider, and
/// distributed cache. Only the mail service is substituted, so tests can read the OTP it would have emailed.
/// </summary>
public class AccountsControllerOtpTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IMailService _mailService;

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
        await _factory.LoginWithNewAccount(_ownerEmail);
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

    private async Task SetEmailVerifiedAsync(string email, bool emailVerified)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        Assert.NotNull(user);
        user.EmailVerified = emailVerified;
        await _userRepository.ReplaceAsync(user);
    }

    private async Task ExpireOtpAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        Assert.NotNull(user);

        var cache = _factory.Services.GetRequiredKeyedService<IDistributedCache>("persistent");
        var otpCacheKey = $"EmailToken_{user.Id}_{user.SecurityStamp}_otp:{user.Email}";

        Assert.NotNull(await cache.GetAsync(otpCacheKey));
        await cache.RemoveAsync(otpCacheKey);
    }

    private static async Task AssertInvalidTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid token", content);
    }
}
