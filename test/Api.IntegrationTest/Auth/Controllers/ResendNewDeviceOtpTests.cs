using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Core.Auth.Enums;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Api.IntegrationTest.Auth.Controllers;

/// <summary>
/// Covers how the new device verification resend endpoint decides which device to scope a code to, running
/// the real service and distributed cache rather than a substitute. <see cref="AccountsControllerTest"/>
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

        var response = await PostResendAsync(factory, user.Email, deviceIdentifier: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertCodeSentAsync(mailService);
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

    private static async Task RecordPendingDeviceAsync(
        ApiApplicationFactory factory, User user, string deviceIdentifier)
    {
        var cache = factory.Services.GetRequiredKeyedService<IFusionCache>(
            NewDeviceVerificationCacheConstants.CacheName);
        await cache.SetAsync(user.Id.ToString(), deviceIdentifier);
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
}
