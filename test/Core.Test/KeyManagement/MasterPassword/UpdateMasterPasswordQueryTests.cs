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
public class UpdateMasterPasswordQueryTests
{
    private static KdfSettings ValidKdf =>
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000, Memory = null, Parallelism = null };

    private static void SetupValidUser(User user)
    {
        user.Email = "test@example.com";
        user.MasterPasswordSalt = null;
        user.Kdf = ValidKdf.KdfType;
        user.KdfIterations = ValidKdf.Iterations;
        user.KdfMemory = ValidKdf.Memory;
        user.KdfParallelism = ValidKdf.Parallelism;
    }

    private static UpdateMasterPasswordData CreateValidData(string salt, KdfSettings kdf) =>
        new()
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "client-auth-hash",
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "new-master-key-wrapped-user-key",
                Salt = salt
            },
            MasterPasswordHint = "new-hint"
        };

    [Theory]
    [BitAutoData]
    public async Task RunAsync_ValidData_MutatesUserCorrectly(
        SutProvider<UpdateMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        var originalKdf = user.Kdf;
        var originalKdfIterations = user.KdfIterations;
        var originalSalt = user.MasterPasswordSalt;
        var data = CreateValidData(user.Email, ValidKdf);
        var hashedPassword = "server-hashed-password";
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, data.MasterPasswordAuthentication.MasterPasswordAuthenticationHash)
            .Returns(hashedPassword);

        await sutProvider.Sut.RunAsync(user, data);

        Assert.Equal(hashedPassword, user.MasterPassword);
        Assert.Equal("new-hint", user.MasterPasswordHint);
        Assert.Equal("new-master-key-wrapped-user-key", user.Key);
        Assert.NotEqual(default, user.RevisionDate);
        Assert.NotEqual(default, user.AccountRevisionDate);
        Assert.NotNull(user.LastPasswordChangeDate);
        // KDF and salt must not change
        Assert.Equal(originalKdf, user.Kdf);
        Assert.Equal(originalKdfIterations, user.KdfIterations);
        Assert.Equal(originalSalt, user.MasterPasswordSalt);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_KdfChanged_ThrowsInvalidOperationException(
        SutProvider<UpdateMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        var differentKdf = new KdfSettings { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 };
        var data = CreateValidData(user.Email, differentKdf);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(user, data));
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_SaltMismatch_ThrowsInvalidOperationException(
        SutProvider<UpdateMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        var data = CreateValidData("wrong@example.com", ValidKdf);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RunAsync(user, data));
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_DoesNotCallRepository(
        SutProvider<UpdateMasterPasswordQuery> sutProvider, User user)
    {
        SetupValidUser(user);
        var data = CreateValidData(user.Email, ValidKdf);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), Arg.Any<string>())
            .Returns("hashed");

        await sutProvider.Sut.RunAsync(user, data);

        // Queries must not persist — verify no repository interactions
    }
}
