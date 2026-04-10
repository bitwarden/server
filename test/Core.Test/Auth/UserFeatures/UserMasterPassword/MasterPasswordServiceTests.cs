using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
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
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;
        var expectedHash = "server-hashed-" + masterPasswordHash;
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, masterPasswordHash)
            .Returns(expectedHash);

        var setInitialData = new SetInitialPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            }
        };

        // Act
        await sutProvider.Sut.OnlyMutateUserSetInitialMasterPasswordAsync(user, setInitialData);

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
    public async Task SetInitialMasterPassword_SetsMasterPasswordHint(User user, string masterPasswordHash, string key, KdfSettings kdf, string salt, string hint)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var setInitialData = new SetInitialPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            },
            MasterPasswordHint = hint
        };

        // Act
        await sutProvider.Sut.OnlyMutateUserSetInitialMasterPasswordAsync(user, setInitialData);

        // Assert
        Assert.Equal(hint, user.MasterPasswordHint);
    }

    [Theory, BitAutoData]
    public async Task SetInitialMasterPassword_ThrowsWhenMasterPasswordAlreadySet(User user, string masterPasswordHash, string key, KdfSettings kdf, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.Key = null;
        user.MasterPasswordSalt = null;

        var setInitialData = new SetInitialPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.OnlyMutateUserSetInitialMasterPasswordAsync(user, setInitialData));
        Assert.Equal("User already has a master password set.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SetInitialMasterPassword_ThrowsWhenKeyAlreadySet(User user, string masterPasswordHash, string key, KdfSettings kdf, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = "existing-key";
        user.MasterPasswordSalt = null;

        var setInitialData = new SetInitialPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.OnlyMutateUserSetInitialMasterPasswordAsync(user, setInitialData));
        Assert.Equal("User already has a key set.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SetInitialMasterPasswordAsync_CallsMutationThenSavesUser(
        User user, string masterPasswordHash, string key, KdfSettings kdf, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
        user.UsesKeyConnector = false;

        var setInitialData = new SetInitialPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            }
        };

        // Act
        await sutProvider.Sut.SetInitialMasterPasswordAndSaveUserAsync(user, setInitialData);

        // Assert: mutation was applied
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(key, user.Key);

        // Assert: user was persisted
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(user);
    }

    // -------------------------------------------------------------------------
    // UpdateMasterPassword
    // -------------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task UpdateMasterPassword_Success(User user, string masterPasswordHash, string key, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.MasterPasswordSalt = salt;
        user.UsesKeyConnector = false;
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var expectedHash = "server-hashed-" + masterPasswordHash;
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, masterPasswordHash)
            .Returns(expectedHash);

        var updateData = new UpdateExistingPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            }
        };

        // Act
        await sutProvider.Sut.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, updateData);

        // Assert
        Assert.Equal(expectedHash, user.MasterPassword);
        Assert.Equal(key, user.Key);
        Assert.NotNull(user.LastPasswordChangeDate);
        // KDF fields must be unchanged
        Assert.Equal(kdf.KdfType, user.Kdf);
        Assert.Equal(kdf.Iterations, user.KdfIterations);
        Assert.Equal(kdf.Memory, user.KdfMemory);
        Assert.Equal(kdf.Parallelism, user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task UpdateMasterPassword_SetsMasterPasswordHint(User user, string masterPasswordHash, string key, string salt, string hint)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.MasterPasswordSalt = salt;
        user.UsesKeyConnector = false;
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };

        var updateData = new UpdateExistingPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            },
            MasterPasswordHint = hint
        };

        // Act
        await sutProvider.Sut.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, updateData);

        // Assert
        Assert.Equal(hint, user.MasterPasswordHint);
    }

    [Theory, BitAutoData]
    public async Task UpdateMasterPassword_ThrowsWhenNoExistingPassword(User user, string masterPasswordHash, string key, KdfSettings kdf, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = null;

        var updateData = new UpdateExistingPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, updateData));
        Assert.Equal("User does not have an existing master password to update.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateMasterPassword_ThrowsWhenKdfMismatch(User user, string masterPasswordHash, string key, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.UsesKeyConnector = false;
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

        var updateData = new UpdateExistingPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = mismatchedKdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = mismatchedKdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sutProvider.Sut.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, updateData));
    }

    [Theory, BitAutoData]
    public async Task UpdateMasterPasswordAsync_CallsMutationThenSavesUser(User user, string masterPasswordHash, string key, string salt)
    {
        // Arrange
        var sutProvider = CreateSutProvider();
        user.MasterPassword = "existing-hash";
        user.MasterPasswordSalt = salt;
        user.UsesKeyConnector = false;
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };

        var updateData = new UpdateExistingPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = key,
                Salt = salt
            }
        };

        // Act
        await sutProvider.Sut.UpdateExistingMasterPasswordAndSaveAsync(user, updateData);

        // Assert: mutation was applied
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(key, user.Key);

        // Assert: user was persisted
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(user);
    }
}
