using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Core.Auth.Enums;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.NewDeviceVerification;

/// <summary>
/// Covers how the new device verification resend endpoint decides which device to scope a code to, running
/// the real service and distributed cache rather than a substitute. <see cref="SubstitutedTwoFactorEmailServiceTests"/>
/// substitutes <see cref="ITwoFactorEmailService"/>, so it can only assert the controller's wiring.
/// </summary>
public class ResendNewDeviceOtpTests
{
    private const string RequestDeviceIdentifier = "request-device-identifier";
    private const string PendingDeviceIdentifier = "pending-device-identifier";
    private const string MasterPasswordHash = "master_password_hash";

    [Fact]
    public async Task ResendNewDeviceOtp_WithDeviceIdentifierHeader_SendsCode()
    {
        var (factory, mailService, user) = await ArrangeAsync();

        var response = await PostResendAsync(factory, user.Email, RequestDeviceIdentifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertCodeSentAsync(mailService);
    }

    // TODO: PM-43465 - Delete this test once every supported client version sends the Device-Identifier
    // header on this request. It covers the fallback to the device the pending code was issued to.
    [Fact]
    public async Task ResendNewDeviceOtp_NoHeader_PendingDeviceRecorded_SendsCode()
    {
        var (factory, mailService, user) = await ArrangeAsync();
        await RecordPendingDeviceAsync(factory, user, PendingDeviceIdentifier);
        mailService.ClearReceivedCalls();

        var response = await PostResendAsync(factory, user.Email, deviceIdentifier: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertCodeSentAsync(mailService);
    }

    // TODO: PM-43465 - Delete this test once mobile sends the Device-Identifier header on this request
    // (PM-43467) and that release has aged out of the support window.
    /// <summary>
    /// The common reason to press resend is that the previous code already expired, so the record naming the
    /// device must outlive the code. Expiry is simulated by evicting the code's cache entry, because the test
    /// host cannot advance the clock — the record lives under its own key with a longer expiration, so a real
    /// timeout removes the same entry this evicts.
    /// </summary>
    [Fact]
    public async Task ResendNewDeviceOtp_NoHeader_CodeExpired_StillSendsCode()
    {
        var (factory, mailService, user) = await ArrangeAsync();
        await RecordPendingDeviceAsync(factory, user, PendingDeviceIdentifier);
        mailService.ClearReceivedCalls();

        await ExpireVerificationCodeAsync(factory, user);

        var response = await PostResendAsync(factory, user.Email, deviceIdentifier: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertCodeSentAsync(mailService);
    }

    [Fact]
    public async Task ResendNewDeviceOtp_OverLongHeader_PendingDeviceRecorded_RejectsAndSendsNothing()
    {
        var (factory, mailService, user) = await ArrangeAsync();
        // A pending device is recorded to show the fallback does not rescue an over-long identifier. The
        // caller would submit the same over-long value when redeeming, so a code scoped to the pending
        // device could not be redeemed — and issuing one would overwrite that device's outstanding code.
        await RecordPendingDeviceAsync(factory, user, PendingDeviceIdentifier);
        mailService.ClearReceivedCalls();

        var response = await PostResendAsync(
            factory, user.Email, new string('a', Device.MaxIdentifierLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await mailService.DidNotReceiveWithAnyArgs().SendTwoFactorEmailAsync(
            default, default, default, default, default, default);
    }

    [Fact]
    public async Task ResendNewDeviceOtp_NoHeader_NoPendingDevice_SendsNothing()
    {
        var (factory, mailService, user) = await ArrangeAsync();

        var response = await PostResendAsync(factory, user.Email, deviceIdentifier: null);

        // Silent 200 so the response does not reveal why nothing was sent.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await mailService.DidNotReceiveWithAnyArgs().SendTwoFactorEmailAsync(
            default, default, default, default, default, default);
    }

    /// <summary>
    /// Builds a factory with only the mail service substituted, so the real two-factor email service and the
    /// real distributed cache both participate. A local factory per test guarantees the substitution is
    /// applied before the host is built.
    /// </summary>
    private static async Task<(ApiApplicationFactory Factory, IMailService MailService, User User)> ArrangeAsync()
    {
        var factory = new ApiApplicationFactory();
        factory.SubstituteService<IMailService>(_ => { });

        var email = $"resend-new-device-otp-{Guid.NewGuid()}@bitwarden.com";
        await factory.LoginWithNewAccount(email, MasterPasswordHash);

        var user = await factory.GetService<IUserRepository>().GetByEmailAsync(email);
        Assert.NotNull(user);

        return (factory, factory.GetService<IMailService>(), user);
    }

    /// <summary>
    /// Issues a real new device verification code for the given device, so the pending-device record the
    /// resend fallback reads is populated exactly as it would be by a genuine challenge.
    /// </summary>
    private static async Task RecordPendingDeviceAsync(
        ApiApplicationFactory factory, User user, string deviceIdentifier)
    {
        var twoFactorEmailService = factory.GetService<ITwoFactorEmailService>();
        await twoFactorEmailService.SendNewDeviceVerificationEmailAsync(user, deviceIdentifier);
    }

    /// <summary>
    /// Evicts the cache entry holding the verification code, leaving any other new device verification state
    /// in place — the same end state a real expiration of that entry reaches.
    /// </summary>
    private static async Task ExpireVerificationCodeAsync(ApiApplicationFactory factory, User user)
    {
        var cache = factory.Services.GetRequiredKeyedService<IDistributedCache>("persistent");
        var codeCacheKey =
            $"NewDeviceVerification_NewDeviceVerificationCode_{user.Id}_{user.SecurityStamp}";

        Assert.NotNull(await cache.GetAsync(codeCacheKey));
        await cache.RemoveAsync(codeCacheKey);
    }

    private static async Task<HttpResponseMessage> PostResendAsync(
        ApiApplicationFactory factory, string email, string? deviceIdentifier)
    {
        using var client = factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/resend-new-device-otp");
        if (deviceIdentifier != null)
        {
            message.Headers.Add("Device-Identifier", deviceIdentifier);
        }

        message.Content = JsonContent.Create(new UnauthenticatedSecretVerificationRequestModel
        {
            Email = email,
            MasterPasswordHash = MasterPasswordHash,
        });

        return await client.SendAsync(message);
    }

    private static async Task AssertCodeSentAsync(IMailService mailService)
    {
        await mailService.Received(1).SendTwoFactorEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            TwoFactorEmailPurpose.NewDeviceVerification);
    }

    /// <summary>
    /// Runs with <see cref="ITwoFactorEmailService"/> substituted, so these tests assert which user and device
    /// the controller passes to the service.
    /// </summary>
    public class SubstitutedTwoFactorEmailServiceTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
    {
        // const so it can be a default parameter value on PostResendNewDeviceOtpAsync.
        private const string _resendDeviceIdentifier = "resend-device-identifier";

        private readonly HttpClient _client;
        private readonly ApiApplicationFactory _factory;
        private readonly IUserRepository _userRepository;
        private readonly ITwoFactorEmailService _twoFactorEmailService;

        private string _ownerEmail = null!;

        public SubstitutedTwoFactorEmailServiceTests(ApiApplicationFactory factory)
        {
            _factory = factory;
            _factory.SubstituteService<ITwoFactorEmailService>(_ => { });
            _client = factory.CreateClient();
            _userRepository = _factory.GetService<IUserRepository>();
            _twoFactorEmailService = _factory.GetService<ITwoFactorEmailService>();
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
        public async Task PostResendNewDeviceOtp_ValidEmailAndSecret_OkAndSendsEmailForRequestingDevice()
        {
            var user = await _userRepository.GetByEmailAsync(_ownerEmail);
            Assert.NotNull(user);

            var response = await PostResendNewDeviceOtpAsync(_ownerEmail, MasterPasswordHash);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await _twoFactorEmailService.Received(1)
                .SendNewDeviceVerificationEmailAsync(
                    Arg.Is<User>(u => u.Id == user.Id), _resendDeviceIdentifier);
        }

        [Fact]
        public async Task PostResendNewDeviceOtp_WrongSecret_SilentlySucceedsWithoutSendingEmail()
        {
            var user = await _userRepository.GetByEmailAsync(_ownerEmail);
            Assert.NotNull(user);

            var response = await PostResendNewDeviceOtpAsync(_ownerEmail, "wrong-master-password-hash");

            // Silent 200 to avoid account enumeration via response shape.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await _twoFactorEmailService.DidNotReceive()
                .SendNewDeviceVerificationEmailAsync(
                    Arg.Is<User>(u => u.Id == user.Id), Arg.Any<string>());
        }

        [Fact]
        public async Task PostResendNewDeviceOtp_UnknownEmail_SilentlySucceeds()
        {
            var response = await PostResendNewDeviceOtpAsync(
                $"does-not-exist-{Guid.NewGuid()}@bitwarden.com",
                MasterPasswordHash);

            // Silent 200 to avoid account enumeration via response shape.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostResendNewDeviceOtp_NoDeviceIdentifier_SilentlySucceedsWithoutSendingEmail()
        {
            var user = await _userRepository.GetByEmailAsync(_ownerEmail);
            Assert.NotNull(user);

            // The code is scoped to the requesting device, so it cannot be issued without one.
            var response = await PostResendNewDeviceOtpAsync(
                _ownerEmail, MasterPasswordHash, deviceIdentifier: null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await _twoFactorEmailService.DidNotReceive()
                .SendNewDeviceVerificationEmailAsync(
                    Arg.Is<User>(u => u.Id == user.Id), Arg.Any<string>());
        }

        private async Task<HttpResponseMessage> PostResendNewDeviceOtpAsync(
            string email, string masterPasswordHash, string? deviceIdentifier = _resendDeviceIdentifier)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/resend-new-device-otp");
            if (deviceIdentifier != null)
            {
                message.Headers.Add("Device-Identifier", deviceIdentifier);
            }
            message.Content = JsonContent.Create(new UnauthenticatedSecretVerificationRequestModel
            {
                Email = email,
                MasterPasswordHash = masterPasswordHash,
            });
            return await _client.SendAsync(message);
        }
    }
}
