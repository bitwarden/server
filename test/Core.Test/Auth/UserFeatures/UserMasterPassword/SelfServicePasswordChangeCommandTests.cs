using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using OneOf;
using Xunit;
using static Bit.Core.Test.Auth.UserFeatures.UserMasterPassword.MasterPasswordTestData;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class SelfServicePasswordChangeCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task ChangePasswordAsync_Success(
        SutProvider<SelfServicePasswordChangeCommand> sutProvider,
        User user, string masterPasswordHash, string masterPasswordHint,
        KdfSettings kdfSettings, string salt, string wrappedKey, string authHash)
    {
        var unlockData = CreateUnlockData(kdfSettings, salt, wrappedKey);
        var authenticationData = CreateAuthenticationData(kdfSettings, salt, authHash);

        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, masterPasswordHash)
            .Returns(true);

        sutProvider.GetDependency<IMasterPasswordService>()
            .SaveUpdateExistingMasterPasswordAsync(user, Arg.Any<UpdateExistingPasswordData>())
            .Returns(OneOf<User, IdentityError[]>.FromT0(user));

        var result = await sutProvider.Sut.ChangePasswordAsync(
            user, masterPasswordHash, unlockData, authenticationData, masterPasswordHint);

        Assert.Equal(IdentityResult.Success, result);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_ChangedPassword);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task ChangePasswordAsync_WrongPassword_ReturnsPasswordMismatch(
        SutProvider<SelfServicePasswordChangeCommand> sutProvider,
        User user, string masterPasswordHash, string masterPasswordHint,
        KdfSettings kdfSettings, string salt, string wrappedKey, string authHash)
    {
        var unlockData = CreateUnlockData(kdfSettings, salt, wrappedKey);
        var authenticationData = CreateAuthenticationData(kdfSettings, salt, authHash);

        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, masterPasswordHash)
            .Returns(false);

        var result = await sutProvider.Sut.ChangePasswordAsync(
            user, masterPasswordHash, unlockData, authenticationData, masterPasswordHint);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordMismatch");

        await sutProvider.GetDependency<IMasterPasswordService>().DidNotReceiveWithAnyArgs()
            .SaveUpdateExistingMasterPasswordAsync(default!, default!);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs()
            .PushLogOutAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task ChangePasswordAsync_MasterPasswordServiceFails_ReturnsErrors(
        SutProvider<SelfServicePasswordChangeCommand> sutProvider,
        User user, string masterPasswordHash, string masterPasswordHint,
        KdfSettings kdfSettings, string salt, string wrappedKey, string authHash)
    {
        var unlockData = CreateUnlockData(kdfSettings, salt, wrappedKey);
        var authenticationData = CreateAuthenticationData(kdfSettings, salt, authHash);
        var identityErrors = new[] { new IdentityError { Code = "TestError", Description = "Test failure" } };

        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, masterPasswordHash)
            .Returns(true);

        sutProvider.GetDependency<IMasterPasswordService>()
            .SaveUpdateExistingMasterPasswordAsync(user, Arg.Any<UpdateExistingPasswordData>())
            .Returns(OneOf<User, IdentityError[]>.FromT1(identityErrors));

        var result = await sutProvider.Sut.ChangePasswordAsync(
            user, masterPasswordHash, unlockData, authenticationData, masterPasswordHint);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "TestError");

        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs()
            .PushLogOutAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task ChangePasswordAsync_PassesCorrectDataToMasterPasswordService(
        SutProvider<SelfServicePasswordChangeCommand> sutProvider,
        User user, string masterPasswordHash, string masterPasswordHint,
        KdfSettings kdfSettings, string salt, string wrappedKey, string authHash)
    {
        var unlockData = CreateUnlockData(kdfSettings, salt, wrappedKey);
        var authenticationData = CreateAuthenticationData(kdfSettings, salt, authHash);

        sutProvider.GetDependency<IUserService>()
            .CheckPasswordAsync(user, masterPasswordHash)
            .Returns(true);

        sutProvider.GetDependency<IMasterPasswordService>()
            .SaveUpdateExistingMasterPasswordAsync(user, Arg.Any<UpdateExistingPasswordData>())
            .Returns(OneOf<User, IdentityError[]>.FromT0(user));

        await sutProvider.Sut.ChangePasswordAsync(
            user, masterPasswordHash, unlockData, authenticationData, masterPasswordHint);

        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .SaveUpdateExistingMasterPasswordAsync(user,
                Arg.Is<UpdateExistingPasswordData>(d =>
                    d.MasterPasswordUnlock == unlockData &&
                    d.MasterPasswordAuthentication == authenticationData &&
                    d.MasterPasswordHint == masterPasswordHint));
    }
}
