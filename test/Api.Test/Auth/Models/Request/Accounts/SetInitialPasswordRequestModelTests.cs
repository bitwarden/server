using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request.Accounts;

public class SetInitialPasswordRequestModelTests
{
    #region V2 Validation Tests

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [InlineData(KdfType.Argon2id, 3, 64, 4)]
    public void Validate_V2Request_WithMatchingKdf_ReturnsNoErrors(KdfType kdfType, int iterations, int? memory, int? parallelism)
    {
        // Arrange
        var kdf = new KdfRequestModel
        {
            KdfType = kdfType,
            Iterations = iterations,
            Memory = memory,
            Parallelism = parallelism
        };

        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            },
            AccountKeys = new AccountKeysRequestModel
            {
                UserKeyEncryptedAccountPrivateKey = "privateKey",
                AccountPublicKey = "publicKey"
            }
        };

        // Act
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_V2Request_WithMismatchedKdfSettings_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 650000 // Different iterations
                },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Contains("KDF settings must be equal", result[0].ErrorMessage);
        var memberNames = result[0].MemberNames.ToList();
        Assert.Equal(2, memberNames.Count);
        Assert.Contains("MasterPasswordAuthentication.Kdf", memberNames);
        Assert.Contains("MasterPasswordUnlock.Kdf", memberNames);
    }

    [Theory]
    [BitAutoData]
    public void Validate_V2Request_WithInvalidAuthenticationKdf_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 1 // Too low
        };

        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("KDF iterations must be between"));
    }

    #endregion

    #region V1 Validation Tests (Obsolete)

    [Theory]
    [BitAutoData]
    public void Validate_V1Request_WithMissingMasterPasswordHash_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            Key = "key",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage.Contains("MasterPasswordHash must be supplied"));
    }

    [Theory]
    [BitAutoData]
    public void Validate_V1Request_WithMissingKey_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHash = "hash",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage.Contains("Key must be supplied"));
    }

    [Theory]
    [BitAutoData]
    public void Validate_V1Request_WithMissingKdf_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHash = "hash",
            Key = "key",
            KdfIterations = 600000
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("Kdf must be supplied"));
    }

    [Theory]
    [BitAutoData]
    public void Validate_V1Request_WithMissingKdfIterations_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHash = "hash",
            Key = "key",
            Kdf = KdfType.PBKDF2_SHA256
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("KdfIterations must be supplied"));
    }

    [Theory]
    [BitAutoData]
    public void Validate_V1Request_WithArgon2idAndMissingMemory_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHash = "hash",
            Key = "key",
            Kdf = KdfType.Argon2id,
            KdfIterations = 3,
            KdfParallelism = 4
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage.Contains("KdfMemory must be supplied when Kdf is Argon2id"));
    }

    [Theory]
    [BitAutoData]
    public void Validate_V1Request_WithArgon2idAndMissingParallelism_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHash = "hash",
            Key = "key",
            Kdf = KdfType.Argon2id,
            KdfIterations = 3,
            KdfMemory = 64
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage.Contains("KdfParallelism must be supplied when Kdf is Argon2id"));
    }

    [Theory]
    [BitAutoData]
    public void Validate_V1Request_WithInvalidKdfSettings_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHash = "hash",
            Key = "key",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5000 // Too low
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("KDF iterations must be between"));
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [InlineData(KdfType.Argon2id, 3, 64, 4)]
    public void Validate_V1Request_WithValidSettings_ReturnsNoErrors(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordHash = "hash",
            Key = "key",
            Kdf = kdfType,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        // Act
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region IsV2Request Tests

    [Theory]
    [BitAutoData]
    public void IsV2Request_WithV2Properties_ReturnsTrue(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        // Act
        var result = model.IsV2Request();

        // Assert
        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public void IsV2Request_WithoutMasterPasswordAuthentication_ReturnsFalse(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        // Act
        var result = model.IsV2Request();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public void IsV2Request_WithoutMasterPasswordUnlock_ReturnsFalse(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            }
        };

        // Act
        var result = model.IsV2Request();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public void IsV2Request_WithV1Properties_ReturnsFalse(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHash = "hash",
            Key = "key",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000
        };

        // Act
        var result = model.IsV2Request();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsTdeSetPasswordRequest Tests

    [Theory]
    [BitAutoData]
    public void IsTdeSetPasswordRequest_WithNullAccountKeys_ReturnsTrue(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            },
            AccountKeys = null
        };

        // Act
        var result = model.IsTdeSetPasswordRequest();

        // Assert
        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public void IsTdeSetPasswordRequest_WithAccountKeys_ReturnsFalse(string orgIdentifier)
    {
        // Arrange
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            },
            AccountKeys = new AccountKeysRequestModel
            {
                UserKeyEncryptedAccountPrivateKey = "privateKey",
                AccountPublicKey = "publicKey"
            }
        };

        // Act
        var result = model.IsTdeSetPasswordRequest();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ToUser Tests (Obsolete)

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [InlineData(KdfType.Argon2id, 3, 64, 4)]
    public void ToUser_WithKeys_MapsPropertiesCorrectly(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange
        var existingUser = new User();
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordHash = "hash",
            MasterPasswordHint = "hint",
            Key = "key",
            Kdf = kdfType,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            Keys = new KeysRequestModel
            {
                PublicKey = "publicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            }
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert
        Assert.Same(existingUser, result);
        Assert.Equal("hint", result.MasterPasswordHint);
        Assert.Equal(kdfType, result.Kdf);
        Assert.Equal(kdfIterations, result.KdfIterations);
        Assert.Equal(kdfMemory, result.KdfMemory);
        Assert.Equal(kdfParallelism, result.KdfParallelism);
        Assert.Equal("key", result.Key);
        Assert.Equal("publicKey", result.PublicKey);
        Assert.Equal("encryptedPrivateKey", result.PrivateKey);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [InlineData(KdfType.Argon2id, 3, 64, 4)]
    public void ToUser_WithoutKeys_MapsPropertiesCorrectly(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange
        var existingUser = new User();
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordHash = "hash",
            MasterPasswordHint = "hint",
            Key = "key",
            Kdf = kdfType,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            Keys = null
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert
        Assert.Same(existingUser, result);
        Assert.Equal("hint", result.MasterPasswordHint);
        Assert.Equal(kdfType, result.Kdf);
        Assert.Equal(kdfIterations, result.KdfIterations);
        Assert.Equal(kdfMemory, result.KdfMemory);
        Assert.Equal(kdfParallelism, result.KdfParallelism);
        Assert.Equal("key", result.Key);
        Assert.Null(result.PublicKey);
        Assert.Null(result.PrivateKey);
    }

    #endregion

    #region ToData Tests

    [Theory]
    [BitAutoData]
    public void ToData_MapsPropertiesCorrectly(string orgIdentifier)
    {
        // Arrange
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 600000
        };

        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHint = "hint",
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            },
            AccountKeys = new AccountKeysRequestModel
            {
                UserKeyEncryptedAccountPrivateKey = "privateKey",
                AccountPublicKey = "publicKey"
            }
        };

        // Act
        var result = model.ToData();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orgIdentifier, result.OrgSsoIdentifier);
        Assert.Equal("hint", result.MasterPasswordHint);
        Assert.NotNull(result.MasterPasswordAuthentication);
        Assert.NotNull(result.MasterPasswordUnlock);
        Assert.NotNull(result.AccountKeys);
        Assert.Equal("authHash", result.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);
        Assert.Equal("wrappedKey", result.MasterPasswordUnlock.MasterKeyWrappedUserKey);
    }

    [Theory]
    [BitAutoData]
    public void ToData_WithNullAccountKeys_MapsCorrectly(string orgIdentifier)
    {
        // Arrange
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 600000
        };

        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            MasterPasswordHint = "hint",
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            },
            AccountKeys = null
        };

        // Act
        var result = model.ToData();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orgIdentifier, result.OrgSsoIdentifier);
        Assert.Null(result.AccountKeys);
    }

    #endregion
}
