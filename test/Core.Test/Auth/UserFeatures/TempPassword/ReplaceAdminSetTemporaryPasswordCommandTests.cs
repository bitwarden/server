using Bit.Core.Auth.UserFeatures.TempPassword;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using OneOf;
using Xunit;
using static Bit.Core.Test.Auth.UserFeatures.UserMasterPassword.MasterPasswordTestData;

namespace Bit.Core.Test.Auth.UserFeatures.TempPassword;

[SutProviderCustomize]
public class ReplaceAdminSetTemporaryPasswordCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task Replace_Success(
        SutProvider<ReplaceAdminSetTemporaryPasswordCommand> sutProvider,
        User user, string masterPasswordHint,
        KdfSettings kdfSettings, string salt, string wrappedKey, string authHash)
    {
        user.ForcePasswordReset = true;
        var unlockData = CreateUnlockData(kdfSettings, salt, wrappedKey);
        var authenticationData = CreateAuthenticationData(kdfSettings, salt, authHash);

        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareUpdateExistingMasterPasswordAsync(user, Arg.Any<UpdateExistingPasswordData>())
            .Returns(OneOf<User, IdentityError[]>.FromT0(user));

        var result = await sutProvider.Sut.Replace(user, unlockData, authenticationData, masterPasswordHint);

        Assert.Equal(IdentityResult.Success, result);
        Assert.False(user.ForcePasswordReset);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedTempPasswordEmailAsync(user.Email, user.Name ?? string.Empty);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_UpdatedTempPassword);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task Replace_NotForcePasswordReset_ThrowsBadRequestException(
        SutProvider<ReplaceAdminSetTemporaryPasswordCommand> sutProvider,
        User user, string masterPasswordHint,
        KdfSettings kdfSettings, string salt, string wrappedKey, string authHash)
    {
        user.ForcePasswordReset = false;
        var unlockData = CreateUnlockData(kdfSettings, salt, wrappedKey);
        var authenticationData = CreateAuthenticationData(kdfSettings, salt, authHash);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.Replace(user, unlockData, authenticationData, masterPasswordHint));

        Assert.Equal("User does not have a temporary password to update.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task Replace_MasterPasswordServiceFails_ReturnsErrors(
        SutProvider<ReplaceAdminSetTemporaryPasswordCommand> sutProvider,
        User user, string masterPasswordHint,
        KdfSettings kdfSettings, string salt, string wrappedKey, string authHash)
    {
        user.ForcePasswordReset = true;
        var unlockData = CreateUnlockData(kdfSettings, salt, wrappedKey);
        var authenticationData = CreateAuthenticationData(kdfSettings, salt, authHash);
        var identityErrors = new[] { new IdentityError { Code = "TestError", Description = "Test failure" } };

        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareUpdateExistingMasterPasswordAsync(user, Arg.Any<UpdateExistingPasswordData>())
            .Returns(OneOf<User, IdentityError[]>.FromT1(identityErrors));

        var result = await sutProvider.Sut.Replace(user, unlockData, authenticationData, masterPasswordHint);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "TestError");
        Assert.True(user.ForcePasswordReset);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendUpdatedTempPasswordEmailAsync(default!, default!);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs()
            .PushLogOutAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task Replace_PassesCorrectDataToMasterPasswordService(
        SutProvider<ReplaceAdminSetTemporaryPasswordCommand> sutProvider,
        User user, string masterPasswordHint,
        KdfSettings kdfSettings, string salt, string wrappedKey, string authHash)
    {
        user.ForcePasswordReset = true;
        var unlockData = CreateUnlockData(kdfSettings, salt, wrappedKey);
        var authenticationData = CreateAuthenticationData(kdfSettings, salt, authHash);

        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareUpdateExistingMasterPasswordAsync(user, Arg.Any<UpdateExistingPasswordData>())
            .Returns(OneOf<User, IdentityError[]>.FromT0(user));

        await sutProvider.Sut.Replace(user, unlockData, authenticationData, masterPasswordHint);

        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareUpdateExistingMasterPasswordAsync(user,
                Arg.Is<UpdateExistingPasswordData>(d =>
                    d.MasterPasswordUnlock == unlockData &&
                    d.MasterPasswordAuthentication == authenticationData &&
                    d.MasterPasswordHint == masterPasswordHint));
    }
}
