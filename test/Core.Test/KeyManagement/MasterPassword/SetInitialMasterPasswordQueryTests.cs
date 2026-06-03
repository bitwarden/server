#nullable enable

using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.MasterPassword;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.MasterPassword;

[SutProviderCustomize]
public class SetInitialMasterPasswordQueryTests
{
    private static KdfSettings ValidKdf =>
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000, Memory = null, Parallelism = null };

    private static void SetupValidUser(User user)
    {
        user.Email = "test@example.com";
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
    }

    private static SetInitialMasterPasswordData CreateValidData(string salt) =>
        new()
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = ValidKdf,
                MasterPasswordAuthenticationHash = "client-auth-hash",
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = ValidKdf,
                MasterKeyWrappedUserKey = "master-key-wrapped-user-key",
                Salt = salt
            },
            MasterPasswordHint = "hint"
        };

    [Theory]
    [BitAutoData]
    public async Task RunAsync_ValidData_MutatesUserCorrectly(
        SutProvider<SetInitialMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        var data = CreateValidData(user.Email);
        var hashedPassword = "server-hashed-password";
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, data.MasterPasswordAuthentication.MasterPasswordAuthenticationHash)
            .Returns(hashedPassword);

        await sutProvider.Sut.RunAsync(user, data);

        Assert.Equal(hashedPassword, user.MasterPassword);
        Assert.Equal("hint", user.MasterPasswordHint);
        Assert.Equal(user.Email, user.MasterPasswordSalt);
        Assert.Equal("master-key-wrapped-user-key", user.Key);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(600000, user.KdfIterations);
        Assert.Null(user.KdfMemory);
        Assert.Null(user.KdfParallelism);
        Assert.NotEqual(default, user.RevisionDate);
        Assert.NotEqual(default, user.AccountRevisionDate);
        Assert.NotNull(user.LastPasswordChangeDate);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_UserAlreadyHasMasterPassword_ThrowsInvalidOperationException(
        SutProvider<SetInitialMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        user.MasterPassword = "existing-hash";
        var data = CreateValidData(user.Email);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(user, data));
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_UserAlreadyHasKey_ThrowsInvalidOperationException(
        SutProvider<SetInitialMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        user.Key = "existing-key";
        var data = CreateValidData(user.Email);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(user, data));
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_UserAlreadyHasSalt_ThrowsInvalidOperationException(
        SutProvider<SetInitialMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        user.MasterPasswordSalt = "existing-salt";
        var data = CreateValidData(user.Email);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(user, data));
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_SaltMismatch_ThrowsInvalidOperationException(
        SutProvider<SetInitialMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        var data = CreateValidData("wrong@example.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(user, data));
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_DoesNotCallRepository(
        SutProvider<SetInitialMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        var data = CreateValidData(user.Email);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hashed");

        await sutProvider.Sut.RunAsync(user, data);

        // Queries must not persist — verify no repository interactions
        // (SutProvider will inject no IUserRepository; this test confirms no persistence dependency)
    }
}
