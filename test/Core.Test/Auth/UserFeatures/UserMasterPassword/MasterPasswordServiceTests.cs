using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

public class MasterPasswordServiceTests
{
    private static SutProvider<MasterPasswordService> CreateSutProvider()
        => new SutProvider<MasterPasswordService>().WithFakeTimeProvider().Create();

    [Theory, BitAutoData]
    public async Task SetInitialMasterPassword_Success(User user, string masterPasswordHash, string key, KdfSettings kdf, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        var expectedHash = "server-hashed-" + masterPasswordHash;
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, masterPasswordHash)
            .Returns(expectedHash);

        // Act
        await sutProvider.Sut.OnlyMutateUserSetInitialMasterPasswordAsync(user, masterPasswordHash, key, kdf, salt);

        // Assert
        Assert.Equal(expectedHash, user.MasterPassword);
        Assert.Equal(key, user.Key);
        Assert.Equal(kdf.KdfType, user.Kdf);
        Assert.Equal(kdf.Iterations, user.KdfIterations);
        Assert.Equal(kdf.Memory, user.KdfMemory);
        Assert.Equal(kdf.Parallelism, user.KdfParallelism);
        Assert.Equal(salt, user.MasterPasswordSalt);
        Assert.NotNull(user.LastPasswordChangeDate);
    }

    [Theory, BitAutoData]
    public async Task SetInitialMasterPassword_SaltNull_DoesNotSetMasterPasswordSalt(User user, string masterPasswordHash, string key, KdfSettings kdf)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        var originalSalt = user.MasterPasswordSalt;

        // Act
        await sutProvider.Sut.OnlyMutateUserSetInitialMasterPasswordAsync(user, masterPasswordHash, key, kdf, null);

        // Assert
        Assert.Equal(originalSalt, user.MasterPasswordSalt);
    }

    [Theory, BitAutoData]
    public async Task SetInitialMasterPassword_ThrowsWhenMasterPasswordAlreadySet(User user, string masterPasswordHash, string key, KdfSettings kdf)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.Key = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.OnlyMutateUserSetInitialMasterPasswordAsync(user, masterPasswordHash, key, kdf));
        Assert.Equal("User already has a master password set.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SetInitialMasterPassword_ThrowsWhenKeyAlreadySet(User user, string masterPasswordHash, string key, KdfSettings kdf)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = "existing-key";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.OnlyMutateUserSetInitialMasterPasswordAsync(user, masterPasswordHash, key, kdf));
        Assert.Equal("User already has a master password set.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SetInitialMasterPasswordAsync_CallsMutationThenCommand(
        User user, string masterPasswordHash, string key, KdfSettings kdf, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;

        // Act
        await sutProvider.Sut.SetInitialMasterPasswordAndSaveUserAsync(user, masterPasswordHash, key, kdf, salt);

        // Assert: mutation was applied
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(key, user.Key);

        // Assert: command was called with the mutated user
        await sutProvider.GetDependency<ISetInitialMasterPasswordStateCommand>()
            .Received(1)
            .ExecuteAsync(user);
    }

    // -------------------------------------------------------------------------
    // UpdateMasterPassword
    // -------------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task UpdateMasterPassword_Success(User user, string masterPasswordHash, string key, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        user.MasterPassword = "existing-hash";
        var expectedHash = "server-hashed-" + masterPasswordHash;
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, masterPasswordHash)
            .Returns(expectedHash);

        // Act
        await sutProvider.Sut.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, masterPasswordHash, key, kdf, salt);

        // Assert
        Assert.Equal(expectedHash, user.MasterPassword);
        Assert.Equal(key, user.Key);
        Assert.Equal(salt, user.MasterPasswordSalt);
        Assert.NotNull(user.LastPasswordChangeDate);
        // KDF fields must be unchanged
        Assert.Equal(kdf.KdfType, user.Kdf);
        Assert.Equal(kdf.Iterations, user.KdfIterations);
        Assert.Equal(kdf.Memory, user.KdfMemory);
        Assert.Equal(kdf.Parallelism, user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task UpdateMasterPassword_SaltNull_DoesNotSetMasterPasswordSalt(User user, string masterPasswordHash, string key)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        user.MasterPassword = "existing-hash";
        var originalSalt = user.MasterPasswordSalt;

        // Act
        await sutProvider.Sut.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, masterPasswordHash, key, kdf, null);

        // Assert
        Assert.Equal(originalSalt, user.MasterPasswordSalt);
    }

    [Theory, BitAutoData]
    public async Task UpdateMasterPassword_ThrowsWhenNoExistingPassword(User user, string masterPasswordHash, string key, KdfSettings kdf)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, masterPasswordHash, key, kdf));
        Assert.Equal("User does not have an existing master password to update.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateMasterPassword_ThrowsWhenKdfMismatch(User user, string masterPasswordHash, string key)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.Kdf = KdfType.PBKDF2_SHA256;
        user.KdfIterations = 600000;
        // Pass KDF settings that differ from user's stored KDF
        var mismatchedKdf = new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sutProvider.Sut.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, masterPasswordHash, key, mismatchedKdf));
    }

    [Theory, BitAutoData]
    public async Task UpdateMasterPasswordAsync_CallsMutationThenCommand(User user, string masterPasswordHash, string key)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        user.MasterPassword = "existing-hash";

        // Act
        await sutProvider.Sut.UpdateMasterPasswordAndSaveAsync(user, masterPasswordHash, key, kdf);

        // Assert: mutation was applied
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(key, user.Key);

        // Assert: command was called with the mutated user
        await sutProvider.GetDependency<IUpdateMasterPasswordStateCommand>()
            .Received(1)
            .ExecuteAsync(user);
    }
}
