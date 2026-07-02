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
    #region Validation Tests (Auth + Unlock Data)

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [InlineData(KdfType.Argon2id, 3, 64, 4)]
    public void Validate_AuthAndUnlockData_WithMatchingKdfAndSalt_ReturnsNoErrors(KdfType kdfType, int iterations, int? memory, int? parallelism)
    {
        // Arrange — uses separate KDF object instances with identical values to verify value equality
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = kdfType,
                    Iterations = iterations,
                    Memory = memory,
                    Parallelism = parallelism
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = kdfType,
                    Iterations = iterations,
                    Memory = memory,
                    Parallelism = parallelism
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
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_AuthAndUnlockData_WithMismatchedKdfSettings_ReturnsValidationError(string orgIdentifier)
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
        Assert.Contains("AuthenticationData and UnlockData must have the same KDF configuration", result[0].ErrorMessage);
        var memberNames = result[0].MemberNames.ToList();
        Assert.Single(memberNames);
        Assert.Contains("Kdf", memberNames);
    }

    [Theory]
    [BitAutoData]
    public void Validate_AuthAndUnlockData_WithMismatchedSalt_ReturnsValidationError(string orgIdentifier)
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
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt1"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt2" // Different salt
            }
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("Invalid master password salt."));
    }

    [Theory]
    [BitAutoData]
    public void Validate_AuthAndUnlockData_WithInvalidAuthenticationKdf_ReturnsValidationError(string orgIdentifier)
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

    #region Validation Tests (Legacy Data) (Obsolete)

    [Theory]
    [BitAutoData]
    public void Validate_LegacyData_WithMissingMasterPasswordHash_ReturnsValidationError(string orgIdentifier)
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
    public void Validate_LegacyData_WithMissingKey_ReturnsValidationError(string orgIdentifier)
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
    public void Validate_LegacyData_WithMissingKdf_ReturnsValidationError(string orgIdentifier)
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
    public void Validate_LegacyData_WithMissingKdfIterations_ReturnsValidationError(string orgIdentifier)
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
    public void Validate_LegacyData_WithArgon2idAndMissingMemory_ReturnsValidationError(string orgIdentifier)
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
    public void Validate_LegacyData_WithArgon2idAndMissingParallelism_ReturnsValidationError(string orgIdentifier)
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
    public void Validate_LegacyData_WithInvalidKdfSettings_ReturnsValidationError(string orgIdentifier)
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
    public void Validate_LegacyData_WithValidSettings_ReturnsNoErrors(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
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

    #region Validation Tests (Cross-Shape)

    // A request must send either AccountKeys (new shape) or Keys (legacy), or neither. It must not send both.
    // This rule fires regardless of whether the request uses the modern (MPAD/MPUD) or legacy (top-level fields)
    // shape — it's a request shape coherence check that runs before either shape-specific validation block.
    [Theory]
    [BitAutoData]
    public void Validate_WithBothAccountKeysAndLegacyKeys_ReturnsValidationError(string orgIdentifier)
    {
        // Arrange — model with both key shapes populated (a request no real client constructs,
        // but defensively rejected to avoid silently dropping one of the keypairs downstream).
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            AccountKeys = new AccountKeysRequestModel
            {
                UserKeyEncryptedAccountPrivateKey = "privateKey",
                AccountPublicKey = "publicKey"
            },
            Keys = new KeysRequestModel
            {
                PublicKey = "publicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            }
        };

        // Act
        var results = model.Validate(new ValidationContext(model)).ToList();

        // Assert — yields a ValidationResult naming both fields as the offending members
        Assert.Contains(results, r =>
            r.ErrorMessage != null &&
            r.ErrorMessage.Contains("Cannot specify both") &&
            r.MemberNames.Contains(nameof(SetInitialPasswordRequestModel.AccountKeys)) &&
            r.MemberNames.Contains(nameof(SetInitialPasswordRequestModel.Keys)));
    }

    #endregion

    #region HasAuthAndUnlockData Tests

    [Theory]
    [BitAutoData]
    public void HasAuthAndUnlockData_WithBothPresent_ReturnsTrue(string orgIdentifier)
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
        var result = model.HasAuthAndUnlockData();

        // Assert
        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public void HasAuthAndUnlockData_WithoutMasterPasswordAuthentication_ReturnsFalse(string orgIdentifier)
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
        var result = model.HasAuthAndUnlockData();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public void HasAuthAndUnlockData_WithoutMasterPasswordUnlock_ReturnsFalse(string orgIdentifier)
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
        var result = model.HasAuthAndUnlockData();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public void HasAuthAndUnlockData_WithLegacyPropertiesOnly_ReturnsFalse(string orgIdentifier)
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
        var result = model.HasAuthAndUnlockData();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsTdeSetPasswordRequest Tests

    [Theory]
    [BitAutoData]
    public void IsTdeSetPasswordRequest_WithBothAccountKeysAndKeysNull_ReturnsTrue(string orgIdentifier)
    {
        // Arrange — TDE user sends no keypair at all (they already have a keypair)
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
            AccountKeys = null,
            Keys = null
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

    [Theory]
    [BitAutoData]
    public void IsTdeSetPasswordRequest_WithLegacyKeysPresent_ReturnsFalse(string orgIdentifier)
    {
        // Arrange — MP JIT request shape: AccountKeys null but legacy Keys populated.
        // Without checking Keys, this would be misclassified as TDE.
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = orgIdentifier,
            AccountKeys = null,
            Keys = new KeysRequestModel
            {
                PublicKey = "publicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
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
        var existingUser = new User { Email = "user@example.com" };
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
        var existingUser = new User { Email = "user@example.com" };
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

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [InlineData(KdfType.Argon2id, 3, 64, 4)]
    public void ToUser_WithMasterPasswordAuthAndUnlock_AndKeys_ReadsKdfAndKeyFromNewData(
        KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange — modern client: MPAD + MPUD + legacy Keys, no top-level legacy KDF/key fields
        var existingUser = new User { Email = "user@example.com" };
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordHint = "hint",
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = kdfType,
                    Iterations = kdfIterations,
                    Memory = kdfMemory,
                    Parallelism = kdfParallelism
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = kdfType,
                    Iterations = kdfIterations,
                    Memory = kdfMemory,
                    Parallelism = kdfParallelism
                },
                MasterKeyWrappedUserKey = "wrappedKeyFromMpud",
                Salt = "salt"
            },
            Keys = new KeysRequestModel
            {
                PublicKey = "publicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            }
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert — KDF mapped from MPAD, user.Key from MPUD, public/private from legacy Keys
        Assert.Same(existingUser, result);
        Assert.Equal("hint", result.MasterPasswordHint);
        Assert.Equal(kdfType, result.Kdf);
        Assert.Equal(kdfIterations, result.KdfIterations);
        Assert.Equal(kdfMemory, result.KdfMemory);
        Assert.Equal(kdfParallelism, result.KdfParallelism);
        Assert.Equal("wrappedKeyFromMpud", result.Key);
        Assert.Equal("publicKey", result.PublicKey);
        Assert.Equal("encryptedPrivateKey", result.PrivateKey);
    }

    [Fact]
    public void ToUser_WithBothNewAndLegacyFieldsSet_PrefersNewData()
    {
        // Arrange — defensive: if a request somehow includes both new and legacy KDF/key fields,
        // ToUser should source from MPUD, not the legacy top-level properties.
        // Uses Argon2id on the new shape so Memory/Parallelism are populated (not null);
        // verifies the new values win for every non-nullable field.
        var existingUser = new User { Email = "user@example.com" };
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordHint = "hint",

            // Legacy top-level (should NOT win)
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000,
            KdfMemory = 999,
            KdfParallelism = 9,
            Key = "legacyKey",

            // New shape (should win)
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.Argon2id,
                    Iterations = 3,
                    Memory = 64,
                    Parallelism = 4
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.Argon2id,
                    Iterations = 3,
                    Memory = 64,
                    Parallelism = 4
                },
                MasterKeyWrappedUserKey = "wrappedKeyFromMpud",
                Salt = "salt"
            }
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert — values came from MPUD, not legacy fields
        Assert.Equal(KdfType.Argon2id, result.Kdf);
        Assert.Equal(3, result.KdfIterations);
        Assert.Equal(64, result.KdfMemory);
        Assert.Equal(4, result.KdfParallelism);
        Assert.Equal("wrappedKeyFromMpud", result.Key);
    }

    [Fact]
    public void ToUser_WithMasterPasswordAuthAndUnlock_AndNullKeys_DoesNotMutateExistingPublicPrivateKey()
    {
        // Arrange — TDE flow: modern client sends MPAD + MPUD with no key material.
        // Existing user has a keypair that must not be replaced.
        var existingUser = new User
        {
            PublicKey = "existingPublicKey",
            PrivateKey = "existingPrivateKey"
        };
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordHint = "hint",
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
                MasterKeyWrappedUserKey = "wrappedKeyFromMpud",
                Salt = "salt"
            },
            Keys = null
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert — KDF/Key mapped from new data, public/private kept intact
        Assert.Equal(KdfType.PBKDF2_SHA256, result.Kdf);
        Assert.Equal("wrappedKeyFromMpud", result.Key);
        Assert.Equal("existingPublicKey", result.PublicKey);
        Assert.Equal("existingPrivateKey", result.PrivateKey);
    }

    [Fact]
    public void ToUser_WithMasterPasswordUnlock_PersistsMpudSalt()
    {
        // Arrange — modern client sends an explicit salt via MPUD; ToUser must persist it
        var existingUser = new User { Email = "user@example.com" };
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "explicitSalt"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "explicitSalt"
            }
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert
        Assert.Equal("explicitSalt", result.MasterPasswordSalt);
    }

    [Fact]
    public void ToUser_WithoutMasterPasswordUnlock_PersistsEmailDerivedSalt()
    {
        // Arrange — older client doesn't send MPUD; ToUser falls back to email-derived V1 salt
        // so the MasterPasswordSalt column is never null after a successful set.
        var existingUser = new User { Email = "User@Example.COM " };
        var model = new SetInitialPasswordRequestModel
        {
            OrgIdentifier = "orgIdentifier",
            MasterPasswordHash = "hash",
            Key = "key",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert — lowercased and trimmed
        Assert.Equal("user@example.com", result.MasterPasswordSalt);
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
