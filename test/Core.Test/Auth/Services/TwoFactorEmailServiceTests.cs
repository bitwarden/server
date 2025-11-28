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

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = IpAddress;

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync("otp:" + user.Email, Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(token));

        var userManager = sutProvider.GetDependency<UserManager<User>>();
        userManager.RegisterTokenProvider(TokenOptions.DefaultEmailProvider, userTwoFactorTokenProvider);

        await sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(email, user.Email, token, IpAddress, deviceType.ToString(),
                TwoFactorEmailPurpose.NewDeviceVerification);
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
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.SendNewDeviceVerificationEmailAsync(null));
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
