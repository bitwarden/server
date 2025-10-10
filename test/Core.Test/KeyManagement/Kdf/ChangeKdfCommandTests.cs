﻿#nullable enable

using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Kdf.Implementations;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Kdf;

[SutProviderCustomize]
public class ChangeKdfCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_ChangesKdfAsync(SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>().UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(IdentityResult.Success));

        var kdf = new KdfSettings { KdfType = Enums.KdfType.Argon2id, Iterations = 4, Memory = 512, Parallelism = 4 };
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "newMasterPassword",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "masterKeyWrappedUserKey",
            Salt = user.GetMasterPasswordSalt()
        };

        await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == user.Id
            && u.Kdf == Enums.KdfType.Argon2id
            && u.KdfIterations == 4
            && u.KdfMemory == 512
            && u.KdfParallelism == 4
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_UserIsNull_ThrowsArgumentNullException(SutProvider<ChangeKdfCommand> sutProvider)
    {
        var kdf = new KdfSettings { KdfType = Enums.KdfType.Argon2id, Iterations = 4, Memory = 512, Parallelism = 4 };
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "newMasterPassword",
            Salt = "salt"
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "masterKeyWrappedUserKey",
            Salt = "salt"
        };

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(null!, "masterPassword", authenticationData, unlockData));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_WrongPassword_ReturnsPasswordMismatch(SutProvider<ChangeKdfCommand> sutProvider,
        User user)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(false));

        var kdf = new KdfSettings { KdfType = Enums.KdfType.Argon2id, Iterations = 4, Memory = 512, Parallelism = 4 };
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "newMasterPassword",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "masterKeyWrappedUserKey",
            Salt = user.GetMasterPasswordSalt()
        };

        var result = await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordMismatch");
    }

    [Theory]
    [BitAutoData]
    public async Task
        ChangeKdfAsync_WithAuthenticationAndUnlockDataAndNoLogoutOnKdfChangeFeatureFlagOff_UpdatesUserCorrectlyAndLogsOut(
            SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        var constantKdf = new KdfSettings
        {
            KdfType = Enums.KdfType.Argon2id,
            Iterations = 5,
            Memory = 1024,
            Parallelism = 4
        };
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = constantKdf,
            MasterPasswordAuthenticationHash = "new-auth-hash",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = constantKdf,
            MasterKeyWrappedUserKey = "new-wrapped-key",
            Salt = user.GetMasterPasswordSalt()
        };
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Task.FromResult(IdentityResult.Success));
        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(false);

        await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == user.Id
            && u.Kdf == constantKdf.KdfType
            && u.KdfIterations == constantKdf.Iterations
            && u.KdfMemory == constantKdf.Memory
            && u.KdfParallelism == constantKdf.Parallelism
            && u.Key == "new-wrapped-key"
        ));
        await sutProvider.GetDependency<IUserService>().Received(1).UpdatePasswordHash(user,
            authenticationData.MasterPasswordAuthenticationHash, validatePassword: true, refreshStamp: true);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushLogOutAsync(user.Id);
        sutProvider.GetDependency<IFeatureService>().Received(1).IsEnabled(FeatureFlagKeys.NoLogoutOnKdfChange);
    }

    [Theory]
    [BitAutoData]
    public async Task
        ChangeKdfAsync_WithAuthenticationAndUnlockDataAndNoLogoutOnKdfChangeFeatureFlagOn_UpdatesUserCorrectlyAndDoesNotLogOut(
            SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        var constantKdf = new KdfSettings
        {
            KdfType = Enums.KdfType.Argon2id,
            Iterations = 5,
            Memory = 1024,
            Parallelism = 4
        };
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = constantKdf,
            MasterPasswordAuthenticationHash = "new-auth-hash",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = constantKdf,
            MasterKeyWrappedUserKey = "new-wrapped-key",
            Salt = user.GetMasterPasswordSalt()
        };
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Task.FromResult(IdentityResult.Success));
        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);

        await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == user.Id
            && u.Kdf == constantKdf.KdfType
            && u.KdfIterations == constantKdf.Iterations
            && u.KdfMemory == constantKdf.Memory
            && u.KdfParallelism == constantKdf.Parallelism
            && u.Key == "new-wrapped-key"
        ));
        await sutProvider.GetDependency<IUserService>().Received(1).UpdatePasswordHash(user,
            authenticationData.MasterPasswordAuthenticationHash, validatePassword: true, refreshStamp: false);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id, false, PushNotificationLogOutReason.KdfChange);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncSettingsAsync(user.Id);
        sutProvider.GetDependency<IFeatureService>().Received(1).IsEnabled(FeatureFlagKeys.NoLogoutOnKdfChange);
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_KdfNotEqualBetweenAuthAndUnlock_ThrowsBadRequestException(
        SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = new KdfSettings
            {
                KdfType = Enums.KdfType.Argon2id,
                Iterations = 4,
                Memory = 512,
                Parallelism = 4
            },
            MasterPasswordAuthenticationHash = "new-auth-hash",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = new KdfSettings { KdfType = Enums.KdfType.PBKDF2_SHA256, Iterations = 100000 },
            MasterKeyWrappedUserKey = "new-wrapped-key",
            Salt = user.GetMasterPasswordSalt()
        };
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_AuthDataSaltMismatch_Throws(SutProvider<ChangeKdfCommand> sutProvider, User user,
        KdfSettings kdf)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "new-auth-hash",
            Salt = "different-salt"
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "new-wrapped-key",
            Salt = user.GetMasterPasswordSalt()
        };
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_UnlockDataSaltMismatch_Throws(SutProvider<ChangeKdfCommand> sutProvider, User user,
        KdfSettings kdf)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "new-auth-hash",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "new-wrapped-key",
            Salt = "different-salt"
        };
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_UpdatePasswordHashFails_ReturnsFailure(SutProvider<ChangeKdfCommand> sutProvider,
        User user)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        var failedResult = IdentityResult.Failed(new IdentityError { Code = "TestFail", Description = "Test fail" });
        sutProvider.GetDependency<IUserService>().UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(failedResult));

        var kdf = new KdfSettings { KdfType = Enums.KdfType.Argon2id, Iterations = 4, Memory = 512, Parallelism = 4 };
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "newMasterPassword",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "masterKeyWrappedUserKey",
            Salt = user.GetMasterPasswordSalt()
        };

        var result = await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_InvalidKdfSettings_ThrowsBadRequestException(
        SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        // Create invalid KDF settings (iterations too low for PBKDF2)
        var invalidKdf = new KdfSettings
        {
            KdfType = Enums.KdfType.PBKDF2_SHA256,
            Iterations = 1000, // This is below the minimum of 600,000
            Memory = null,
            Parallelism = null
        };

        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = invalidKdf,
            MasterPasswordAuthenticationHash = "new-auth-hash",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = invalidKdf,
            MasterKeyWrappedUserKey = "new-wrapped-key",
            Salt = user.GetMasterPasswordSalt()
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData));

        Assert.Equal("KDF settings are invalid.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_InvalidArgon2Settings_ThrowsBadRequestException(
        SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        // Create invalid Argon2 KDF settings (memory too high)
        var invalidKdf = new KdfSettings
        {
            KdfType = Enums.KdfType.Argon2id,
            Iterations = 3, // Valid
            Memory = 2048, // This is above the maximum of 1024
            Parallelism = 4 // Valid
        };

        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = invalidKdf,
            MasterPasswordAuthenticationHash = "new-auth-hash",
            Salt = user.GetMasterPasswordSalt()
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = invalidKdf,
            MasterKeyWrappedUserKey = "new-wrapped-key",
            Salt = user.GetMasterPasswordSalt()
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", authenticationData, unlockData));

        Assert.Equal("KDF settings are invalid.", exception.Message);
    }
}
