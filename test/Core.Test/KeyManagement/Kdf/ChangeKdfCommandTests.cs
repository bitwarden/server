#nullable enable

using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Kdf.Implementations;
using Bit.Core.KeyManagement.Models.Data;
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
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>().UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>()).Returns(Task.FromResult(IdentityResult.Success));

        await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", "newMasterPassword", "masterKeyWrappedUserKey", new KdfSettings
        {
            KdfType = Enums.KdfType.Argon2id,
            Iterations = 4,
            Memory = 512,
            Parallelism = 4
        }, null, null);

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
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(null!, "masterPassword", "newMasterPassword", "masterKeyWrappedUserKey", new KdfSettings
            {
                KdfType = Enums.KdfType.Argon2id,
                Iterations = 4,
                Memory = 512,
                Parallelism = 4
            }, null, null));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_WrongPassword_ReturnsPasswordMismatch(SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(Task.FromResult(false));
        var result = await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", "newMasterPassword", "masterKeyWrappedUserKey", new KdfSettings
        {
            KdfType = Enums.KdfType.Argon2id,
            Iterations = 4,
            Memory = 512,
            Parallelism = 4
        }, null, null);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordMismatch");
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_WithAuthenticationAndUnlockData_UpdatesUserCorrectly(SutProvider<ChangeKdfCommand> sutProvider, User user)
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
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>().UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>()).Returns(Task.FromResult(IdentityResult.Success));

        await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", "should-be-overwritten", "should-be-overwritten", constantKdf, authenticationData, unlockData);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == user.Id
            && u.Kdf == constantKdf.KdfType
            && u.KdfIterations == constantKdf.Iterations
            && u.KdfMemory == constantKdf.Memory
            && u.KdfParallelism == constantKdf.Parallelism
            && u.Key == "new-wrapped-key"
        ));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_KdfNotEqualBetweenAuthAndUnlock_ThrowsBadRequestException(SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = new KdfSettings { KdfType = Enums.KdfType.Argon2id, Iterations = 4, Memory = 512, Parallelism = 4 },
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
            await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", "should-be-overwritten", "should-be-overwritten",
                authenticationData.Kdf, authenticationData, unlockData));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_AuthDataSaltMismatch_Throws(SutProvider<ChangeKdfCommand> sutProvider, User user, KdfSettings kdf)
    {
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
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", "should-be-overwritten", "should-be-overwritten", kdf, authenticationData, unlockData));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_UnlockDataSaltMismatch_Throws(SutProvider<ChangeKdfCommand> sutProvider, User user, KdfSettings kdf)
    {
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
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", "should-be-overwritten", "should-be-overwritten", kdf, authenticationData, unlockData));
    }

    [Theory]
    [BitAutoData]
    public async Task ChangeKdfAsync_UpdatePasswordHashFails_ReturnsFailure(SutProvider<ChangeKdfCommand> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(Task.FromResult(true));
        var failedResult = IdentityResult.Failed(new IdentityError { Code = "TestFail", Description = "Test fail" });
        sutProvider.GetDependency<IUserService>().UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>()).Returns(Task.FromResult(failedResult));

        var result = await sutProvider.Sut.ChangeKdfAsync(user, "masterPassword", "newMasterPassword", "masterKeyWrappedUserKey", new KdfSettings
        {
            KdfType = Enums.KdfType.Argon2id,
            Iterations = 4,
            Memory = 512,
            Parallelism = 4
        }, null, null);

        Assert.False(result.Succeeded);
    }

}
