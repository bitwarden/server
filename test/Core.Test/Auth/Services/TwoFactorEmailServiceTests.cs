using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Core.Auth.Enums;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Services;

[SutProviderCustomize]
public class TwoFactorEmailServiceTests
{
    private const string TokenProviderName = "TwoFactorEmail";
    private const string LoginPurpose = "LoginCode";
    private const string SetupPurpose = "SetupCode";
    private const string DeviceIdentifier = "device-identifier";
    private const string Token = "123456";

    /// <summary>
    /// A login code is issued under the login purpose, bound to the requesting device, and emailed to the user's
    /// two-factor address.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendTwoFactorLoginEmailAsync_Success(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var email = user.Email.ToLowerInvariant();
        var ipAddress = "1.1.1.1";
        var deviceType = DeviceType.Android;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = ipAddress;

        EnrollInEmailTwoFactor(user, email);
        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .GenerateTokenAsync(TokenProviderName, LoginPurpose, UniqueIdentifier(user), DeviceIdentifier)
            .Returns(Token);

        await sutProvider.Sut.SendTwoFactorLoginEmailAsync(user, DeviceIdentifier);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(email, user.Email, Token, ipAddress, deviceType.ToString(),
                TwoFactorEmailPurpose.Login);
    }

    /// <summary>
    /// A login code needs a device to bind to; without one nothing is issued or emailed.
    /// </summary>
    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task SendTwoFactorLoginEmailAsync_NoDeviceIdentifier_ThrowsAndIssuesNoCode(
        string deviceIdentifier, SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        EnrollInEmailTwoFactor(user, user.Email);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sutProvider.Sut.SendTwoFactorLoginEmailAsync(user, deviceIdentifier));

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceiveWithAnyArgs()
            .GenerateTokenAsync(default, default, default, default);
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceiveWithAnyArgs()
            .SendTwoFactorEmailAsync(default, default, default, default, default, default);
    }

    /// <summary>
    /// A setup code is issued under the setup purpose, separate from login codes, bound to the requesting device,
    /// and emailed to the address being set up.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendTwoFactorSetupEmailAsync_Success(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var email = user.Email.ToLowerInvariant();
        var ipAddress = "1.1.1.1";
        var deviceType = DeviceType.Android;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = ipAddress;

        EnrollInEmailTwoFactor(user, email);
        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .GenerateTokenAsync(TokenProviderName, SetupPurpose, UniqueIdentifier(user), DeviceIdentifier)
            .Returns(Token);

        await sutProvider.Sut.SendTwoFactorSetupEmailAsync(user, DeviceIdentifier);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(email, user.Email, Token, ipAddress, deviceType.ToString(),
                TwoFactorEmailPurpose.Setup);
    }

    /// <summary>
    /// Without a device, a setup code is still issued, bound to no device.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendTwoFactorSetupEmailAsync_NoDeviceIdentifier_IssuesCodeBoundToNoDevice(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        EnrollInEmailTwoFactor(user, user.Email);

        await sutProvider.Sut.SendTwoFactorSetupEmailAsync(user, null);

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .Received(1)
            .GenerateTokenAsync(TokenProviderName, SetupPurpose, UniqueIdentifier(user), null);
    }

    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_Success(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var email = user.Email.ToLowerInvariant();
        var IpAddress = "1.1.1.1";
        var deviceType = DeviceType.Android;
        var deviceIdentifier = "device-identifier";
        var code = "123456";

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = IpAddress;

        sutProvider.GetDependency<INewDeviceVerificationOtpStore>()
            .IssueAsync(user, deviceIdentifier)
            .Returns(code);

        await sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user, deviceIdentifier);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(email, user.Email, code, IpAddress, deviceType.ToString(),
                TwoFactorEmailPurpose.NewDeviceVerification);
    }

    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_ExceptionBecauseNoDeviceIdentifier_DoesNotIssueCode(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user, " "));

        await sutProvider.GetDependency<INewDeviceVerificationOtpStore>()
            .DidNotReceiveWithAnyArgs()
            .IssueAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task VerifyNewDeviceVerificationOtpAsync_DelegatesToStore(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";
        var otp = "123456";

        sutProvider.GetDependency<INewDeviceVerificationOtpStore>()
            .ValidateAndConsumeAsync(user, deviceIdentifier, otp)
            .Returns(true);

        Assert.True(await sutProvider.Sut.VerifyNewDeviceVerificationOtpAsync(user, deviceIdentifier, otp));
    }

    [Theory, BitAutoData]
    public async Task VerifyNewDeviceVerificationOtpAsync_NoDeviceIdentifier_Throws(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.VerifyNewDeviceVerificationOtpAsync(user, " ", "123456"));
    }

    [Theory, BitAutoData]
    public async Task GetPendingNewDeviceVerificationDeviceIdentifierAsync_DelegatesToStore(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";

        sutProvider.GetDependency<INewDeviceVerificationOtpStore>()
            .GetPendingDeviceIdentifierAsync(user)
            .Returns(deviceIdentifier);

        Assert.Equal(
            deviceIdentifier,
            await sutProvider.Sut.GetPendingNewDeviceVerificationDeviceIdentifierAsync(user));
    }

    /// <summary>
    /// A user without an email two-factor provider gets no code.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendTwoFactorLoginEmailAsync_ExceptionBecauseNoProviderOnUser(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        user.TwoFactorProviders = null;

        await Assert.ThrowsAsync<ArgumentNullException>("No email.",
            () => sutProvider.Sut.SendTwoFactorLoginEmailAsync(user, DeviceIdentifier));
        await AssertNoCodeIssuedAsync(sutProvider);
    }

    /// <summary>
    /// A user whose email two-factor provider has no metadata gets no code.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendTwoFactorLoginEmailAsync_ExceptionBecauseNoProviderMetadataOnUser(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = null,
                Enabled = true
            }
        });

        await Assert.ThrowsAsync<ArgumentNullException>("No email.",
            () => sutProvider.Sut.SendTwoFactorLoginEmailAsync(user, DeviceIdentifier));
        await AssertNoCodeIssuedAsync(sutProvider);
    }

    /// <summary>
    /// A user whose email two-factor provider has no email address gets no code.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendTwoFactorLoginEmailAsync_ExceptionBecauseNoProviderEmailMetadataOnUser(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["qweqwe"] = user.Email.ToLowerInvariant() },
                Enabled = true
            }
        });

        await Assert.ThrowsAsync<ArgumentNullException>("No email.",
            () => sutProvider.Sut.SendTwoFactorLoginEmailAsync(user, DeviceIdentifier));
        await AssertNoCodeIssuedAsync(sutProvider);
    }

    /// <summary>
    /// A blank two-factor email address counts as no address, so no code is issued for it.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendTwoFactorLoginEmailAsync_ExceptionBecauseBlankProviderEmailOnUser(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        EnrollInEmailTwoFactor(user, " ");

        await Assert.ThrowsAsync<ArgumentNullException>("No email.",
            () => sutProvider.Sut.SendTwoFactorLoginEmailAsync(user, DeviceIdentifier));
        await AssertNoCodeIssuedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_ExceptionBecauseUserNull(SutProvider<TwoFactorEmailService> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sutProvider.Sut.SendNewDeviceVerificationEmailAsync(null, "device-identifier"));
    }

    /// <summary>
    /// The email names the requesting client's device type.
    /// </summary>
    [Theory]
    [BitAutoData(DeviceType.UnknownBrowser, "Unknown Browser")]
    [BitAutoData(DeviceType.Android, "Android")]
    public async Task SendTwoFactorLoginEmailAsync_DeviceMatches(DeviceType deviceType, string deviceTypeName,
        SutProvider<TwoFactorEmailService> sutProvider,
        User user)
    {
        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = "1.1.1.1";

        EnrollInEmailTwoFactor(user, user.Email.ToLowerInvariant());

        await sutProvider.Sut.SendTwoFactorLoginEmailAsync(user, DeviceIdentifier);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), deviceTypeName, TwoFactorEmailPurpose.Login);
    }

    /// <summary>
    /// With no known device type, the email names an unknown browser.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendTwoFactorLoginEmailAsync_NullDeviceTypeShouldSendUnkownBrowserType(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        sutProvider.GetDependency<ICurrentContext>().DeviceType = null;
        EnrollInEmailTwoFactor(user, user.Email.ToLowerInvariant());

        await sutProvider.Sut.SendTwoFactorLoginEmailAsync(user, DeviceIdentifier);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), "Unknown Browser", Arg.Any<TwoFactorEmailPurpose>());
    }

    /// <summary>
    /// A login code is verified under the login purpose against the device submitting it, and the provider's
    /// answer is returned.
    /// </summary>
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task VerifyTwoFactorLoginTokenAsync_ReturnsProviderResultForLoginPurposeAndDevice(
        bool providerResult, SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .ValidateTokenAsync(Token, TokenProviderName, LoginPurpose, UniqueIdentifier(user), DeviceIdentifier)
            .Returns(providerResult);

        Assert.Equal(providerResult, await sutProvider.Sut.VerifyTwoFactorLoginTokenAsync(user, DeviceIdentifier, Token));
    }

    /// <summary>
    /// A login code never verifies without a device, and the provider is not asked.
    /// </summary>
    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task VerifyTwoFactorLoginTokenAsync_NoDeviceIdentifier_FalseWithoutCheckingCode(
        string deviceIdentifier, SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        Assert.False(await sutProvider.Sut.VerifyTwoFactorLoginTokenAsync(user, deviceIdentifier, Token));

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceiveWithAnyArgs()
            .ValidateTokenAsync(default, default, default, default, default);
    }

    /// <summary>
    /// A blank login code never verifies, and the provider is not asked.
    /// </summary>
    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    public async Task VerifyTwoFactorLoginTokenAsync_NoToken_FalseWithoutCheckingCode(
        string token, SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        Assert.False(await sutProvider.Sut.VerifyTwoFactorLoginTokenAsync(user, DeviceIdentifier, token));

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceiveWithAnyArgs()
            .ValidateTokenAsync(default, default, default, default, default);
    }

    [Theory, BitAutoData]
    public async Task VerifyTwoFactorLoginTokenAsync_NullUser_Throws(SutProvider<TwoFactorEmailService> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sutProvider.Sut.VerifyTwoFactorLoginTokenAsync(null, DeviceIdentifier, Token));
    }

    /// <summary>
    /// A setup code is verified under the setup purpose against the submitting device, including no device, and
    /// the provider's answer is returned.
    /// </summary>
    [Theory]
    [BitAutoData(DeviceIdentifier, true)]
    [BitAutoData(DeviceIdentifier, false)]
    [BitAutoData((string)null, true)]
    public async Task VerifyTwoFactorSetupTokenAsync_ReturnsProviderResultForSetupPurposeAndDevice(
        string deviceIdentifier, bool providerResult, SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .ValidateTokenAsync(Token, TokenProviderName, SetupPurpose, UniqueIdentifier(user), deviceIdentifier)
            .Returns(providerResult);

        Assert.Equal(providerResult,
            await sutProvider.Sut.VerifyTwoFactorSetupTokenAsync(user, deviceIdentifier, Token));
    }

    /// <summary>
    /// A blank setup code never verifies, and the provider is not asked.
    /// </summary>
    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    public async Task VerifyTwoFactorSetupTokenAsync_NoToken_FalseWithoutCheckingCode(
        string token, SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        Assert.False(await sutProvider.Sut.VerifyTwoFactorSetupTokenAsync(user, DeviceIdentifier, token));

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceiveWithAnyArgs()
            .ValidateTokenAsync(default, default, default, default, default);
    }

    [Theory, BitAutoData]
    public async Task VerifyTwoFactorSetupTokenAsync_NullUser_Throws(SutProvider<TwoFactorEmailService> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sutProvider.Sut.VerifyTwoFactorSetupTokenAsync(null, DeviceIdentifier, Token));
    }

    private static void EnrollInEmailTwoFactor(User user, string email)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = email },
                Enabled = true
            }
        });
    }

    private static string UniqueIdentifier(User user) => $"{user.Id}_{user.SecurityStamp}";

    private static async Task AssertNoCodeIssuedAsync(SutProvider<TwoFactorEmailService> sutProvider)
    {
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceiveWithAnyArgs()
            .GenerateTokenAsync(default, default, default, default);
    }
}
