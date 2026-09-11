using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Core.Auth.Enums;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Auth.Services;

[SutProviderCustomize]
public class TwoFactorEmailServiceTests
{
    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_Success(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var email = user.Email.ToLowerInvariant();
        var token = "thisisatokentocompare";
        var IpAddress = "1.1.1.1";
        var deviceType = DeviceType.Android;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = IpAddress;

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync("TwoFactor", Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(token));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), userTwoFactorTokenProvider);

        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = email },
                Enabled = true
            }
        });
        await sutProvider.Sut.SendTwoFactorEmailAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(email, user.Email, token, IpAddress, deviceType.ToString(),
                TwoFactorEmailPurpose.Login);
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorSetupEmailAsync_Success(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var email = user.Email.ToLowerInvariant();
        var token = "thisisatokentocompare";
        var IpAddress = "1.1.1.1";
        var deviceType = DeviceType.Android;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = IpAddress;

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync("TwoFactor", Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(token));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), userTwoFactorTokenProvider);

        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = email },
                Enabled = true
            }
        });

        await sutProvider.Sut.SendTwoFactorSetupEmailAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(email, user.Email, token, IpAddress, deviceType.ToString(),
                TwoFactorEmailPurpose.Setup);
    }

    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_Success(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var email = user.Email.ToLowerInvariant();
        var token = "thisisatokentocompare";
        var IpAddress = "1.1.1.1";
        var deviceType = DeviceType.Android;
        var deviceIdentifier = "device-identifier";

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = IpAddress;

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync(NewDeviceOtpPurpose(deviceIdentifier), Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(token));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(TokenOptions.DefaultEmailProvider, userTwoFactorTokenProvider);

        await sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user, deviceIdentifier);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(email, user.Email, token, IpAddress, deviceType.ToString(),
                TwoFactorEmailPurpose.NewDeviceVerification);
    }

    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_DifferentDevice_GeneratesDifferentToken(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var firstDeviceIdentifier = "first-device-identifier";
        var secondDeviceIdentifier = "second-device-identifier";
        var firstToken = "111111";
        var secondToken = "222222";

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync(NewDeviceOtpPurpose(firstDeviceIdentifier), Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(firstToken));
        userTwoFactorTokenProvider
            .GenerateAsync(NewDeviceOtpPurpose(secondDeviceIdentifier), Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(secondToken));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(TokenOptions.DefaultEmailProvider, userTwoFactorTokenProvider);

        await sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user, firstDeviceIdentifier);
        await sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user, secondDeviceIdentifier);

        var mailService = sutProvider.GetDependency<IMailService>();
        await mailService.Received(1).SendTwoFactorEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), firstToken, Arg.Any<string>(), Arg.Any<string>(),
            TwoFactorEmailPurpose.NewDeviceVerification);
        await mailService.Received(1).SendTwoFactorEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), secondToken, Arg.Any<string>(), Arg.Any<string>(),
            TwoFactorEmailPurpose.NewDeviceVerification);
    }

    [Theory, BitAutoData]
    public async Task VerifyNewDeviceVerificationOtpAsync_SameDevice_ReturnsTrue(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";
        var otp = "123456";

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .ValidateAsync(NewDeviceOtpPurpose(deviceIdentifier), otp, Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(TokenOptions.DefaultEmailProvider, userTwoFactorTokenProvider);

        Assert.True(await sutProvider.Sut.VerifyNewDeviceVerificationOtpAsync(user, deviceIdentifier, otp));
    }

    [Theory, BitAutoData]
    public async Task VerifyNewDeviceVerificationOtpAsync_DifferentDevice_ReturnsFalse(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var issuedForDeviceIdentifier = "issued-for-device-identifier";
        var submittingDeviceIdentifier = "submitting-device-identifier";
        var otp = "123456";

        // The token only validates under the purpose it was issued for; every other purpose misses.
        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .ValidateAsync(NewDeviceOtpPurpose(issuedForDeviceIdentifier), otp, Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(TokenOptions.DefaultEmailProvider, userTwoFactorTokenProvider);

        Assert.False(
            await sutProvider.Sut.VerifyNewDeviceVerificationOtpAsync(user, submittingDeviceIdentifier, otp));
    }

    [Theory, BitAutoData]
    public async Task VerifyNewDeviceVerificationOtpAsync_AccountOtpPurpose_ReturnsFalse(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";
        var otp = "123456";

        // A code minted for account secret verification must not satisfy new device verification.
        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .ValidateAsync("otp:" + user.Email, otp, Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(TokenOptions.DefaultEmailProvider, userTwoFactorTokenProvider);

        Assert.False(await sutProvider.Sut.VerifyNewDeviceVerificationOtpAsync(user, deviceIdentifier, otp));
    }

    [Theory, BitAutoData]
    public async Task VerifyNewDeviceVerificationOtpAsync_NoDeviceIdentifier_Throws(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.VerifyNewDeviceVerificationOtpAsync(user, " ", "123456"));
    }

    // TODO: PM-43465 - Delete the three pending-device tests below once every supported client version sends
    // the Device-Identifier header on the new device verification resend request.
    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_RecordsPendingDevice(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .GenerateAsync(Arg.Any<string>(), Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult("123456"));
        sutProvider.GetDependency<UserManager<User>>()
            .RegisterTokenProvider(TokenOptions.DefaultEmailProvider, userTwoFactorTokenProvider);

        await sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user, deviceIdentifier);

        await sutProvider.GetDependency<IFusionCache>().Received(1).SetAsync(
            user.Id.ToString(),
            deviceIdentifier,
            Arg.Any<FusionCacheEntryOptions>(),
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task GetPendingNewDeviceVerificationDeviceIdentifierAsync_Cached_ReturnsDevice(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";
        sutProvider.GetDependency<IFusionCache>()
            .GetOrDefaultAsync<string>(
                user.Id.ToString(),
                Arg.Any<string>(),
                Arg.Any<FusionCacheEntryOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(deviceIdentifier);

        Assert.Equal(
            deviceIdentifier,
            await sutProvider.Sut.GetPendingNewDeviceVerificationDeviceIdentifierAsync(user));
    }

    [Theory, BitAutoData]
    public async Task GetPendingNewDeviceVerificationDeviceIdentifierAsync_NotCached_ReturnsNull(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        sutProvider.GetDependency<IFusionCache>()
            .GetOrDefaultAsync<string>(
                user.Id.ToString(),
                Arg.Any<string>(),
                Arg.Any<FusionCacheEntryOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(null as string);

        Assert.Null(await sutProvider.Sut.GetPendingNewDeviceVerificationDeviceIdentifierAsync(user));
    }

    /// <summary>
    /// Mirrors the purpose the service builds internally. Kept local to the tests so a change to the
    /// production purpose shows up as a failure here rather than passing silently.
    /// </summary>
    private static string NewDeviceOtpPurpose(string deviceIdentifier)
    {
        return "new_device_otp:" + deviceIdentifier;
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderOnUser(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        user.TwoFactorProviders = null;

        await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderMetadataOnUser(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = null,
                Enabled = true
            }
        });

        await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderEmailMetadataOnUser(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["qweqwe"] = user.Email.ToLowerInvariant() },
                Enabled = true
            }
        });

        await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
    }

    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_ExceptionBecauseUserNull(SutProvider<TwoFactorEmailService> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sutProvider.Sut.SendNewDeviceVerificationEmailAsync(null, "device-identifier"));
    }

    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_ExceptionBecauseNoDeviceIdentifier(
        SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user, " "));
    }

    [Theory]
    [BitAutoData(DeviceType.UnknownBrowser, "Unknown Browser")]
    [BitAutoData(DeviceType.Android, "Android")]
    public async Task SendTwoFactorEmailAsync_DeviceMatches(DeviceType deviceType, string deviceTypeName,
        SutProvider<TwoFactorEmailService> sutProvider,
        User user)
    {
        var email = user.Email.ToLowerInvariant();
        var token = "thisisatokentocompare";
        var IpAddress = "1.1.1.1";

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = IpAddress;

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync("TwoFactor", Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(token));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), userTwoFactorTokenProvider);

        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = email },
                Enabled = true
            }
        });

        await sutProvider.Sut.SendTwoFactorEmailAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), deviceTypeName, TwoFactorEmailPurpose.Login);
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_NullDeviceTypeShouldSendUnkownBrowserType(SutProvider<TwoFactorEmailService> sutProvider, User user)
    {
        var email = user.Email.ToLowerInvariant();
        var token = "thisisatokentocompare";
        var IpAddress = "1.1.1.1";

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync("TwoFactor", Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(token));

        var context = Substitute.For<ICurrentContext>();
        context.DeviceType = null;
        context.IpAddress = IpAddress;

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), userTwoFactorTokenProvider);

        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = email },
                Enabled = true
            }
        });

        await sutProvider.Sut.SendTwoFactorEmailAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), "Unknown Browser", Arg.Any<TwoFactorEmailPurpose>());
    }
}
