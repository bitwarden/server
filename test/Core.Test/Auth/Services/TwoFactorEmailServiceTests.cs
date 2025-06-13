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
        var deviceType = "Android";

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync("TwoFactor", Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(token));

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = DeviceType.Android;
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
            .SendTwoFactorEmailAsync(email, user.Email, token, IpAddress, deviceType,
                TwoFactorEmailPurpose.Login);
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
    public async Task SendNewDeviceVerificationEmailAsync_DeviceMatches(DeviceType deviceType, string deviceTypeName,
        User user)
    {
        var sutProvider = new SutProvider<TwoFactorEmailService>();

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = deviceType;
        context.IpAddress = "1.1.1.1";

        await sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), deviceTypeName, Arg.Any<TwoFactorEmailPurpose>());
    }

    [Theory, BitAutoData]
    public async Task SendNewDeviceVerificationEmailAsync_NullDeviceTypeShouldSendUnkownBrowserType(User user)
    {
        var sutProvider = new SutProvider<TwoFactorEmailService>();

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.DeviceType = null;
        context.IpAddress = "1.1.1.1";

        await sutProvider.Sut.SendNewDeviceVerificationEmailAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), "Unknown Browser", Arg.Any<TwoFactorEmailPurpose>());
    }


    // [Theory, BitAutoData]
    // public async Task ResendNewDeviceVerificationEmail_UserNull_SendTwoFactorEmailAsyncNotCalled(
    //     SutProvider<UserService> sutProvider, string email, string secret)
    // {
    //     sutProvider.GetDependency<IUserRepository>()
    //         .GetByEmailAsync(email)
    //         .Returns(null as User);

    //     await sutProvider.Sut.ResendNewDeviceVerificationEmail(email, secret);

    //     await sutProvider.GetDependency<IMailService>()
    //         .DidNotReceive()
    //         .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    // }

    // [Theory, BitAutoData]
    // public async Task ResendNewDeviceVerificationEmail_SecretNotValid_SendTwoFactorEmailAsyncNotCalled(
    //  SutProvider<UserService> sutProvider, string email, string secret)
    // {
    //     sutProvider.GetDependency<IUserRepository>()
    //         .GetByEmailAsync(email)
    //         .Returns(null as User);

    //     await sutProvider.Sut.ResendNewDeviceVerificationEmail(email, secret);

    //     await sutProvider.GetDependency<IMailService>()
    //         .DidNotReceive()
    //         .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    // }

    //     [Theory, BitAutoData]
    // public async Task ResendNewDeviceVerificationEmail_SendsToken_Success(User user)
    // {
    //     // Arrange
    //     var testPassword = "test_password";
    //     SetupUserAndDevice(user, true);

    //     var sutProvider = new SutProvider<TwoFactorEmailService>();

    //     // Setup the fake password verification
    //     sutProvider
    //         .GetDependency<IUserPasswordStore<User>>()
    //         .GetPasswordHashAsync(user, Arg.Any<CancellationToken>())
    //         .Returns((ci) =>
    //         {
    //             return Task.FromResult("hashed_test_password");
    //         });

    //     sutProvider.GetDependency<IPasswordHasher<User>>()
    //         .VerifyHashedPassword(user, "hashed_test_password", testPassword)
    //         .Returns((ci) =>
    //         {
    //             return PasswordVerificationResult.Success;
    //         });

    //     sutProvider.GetDependency<IUserRepository>()
    //         .GetByEmailAsync(user.Email)
    //         .Returns(user);

    //     var context = sutProvider.GetDependency<ICurrentContext>();
    //     context.DeviceType = DeviceType.Android;
    //     context.IpAddress = "1.1.1.1";

    //     await sutProvider.Sut.ResendNewDeviceVerificationEmail(user.Email, testPassword);

    //     await sutProvider.GetDependency<IMailService>()
    //         .Received(1)
    //         .SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());

    // }
}