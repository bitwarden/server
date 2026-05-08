using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request.Accounts;

public class PasswordRequestModelTests
{
    [Theory]
    [BitAutoData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [BitAutoData(KdfType.Argon2id, 3, 64, 4)]
    public void Validate_NewPayloadsOnly_NoErrors(
        KdfType kdfType, int iterations, int? memory, int? parallelism)
    {
        var kdf = new KdfRequestModel
        {
            KdfType = kdfType,
            Iterations = iterations,
            Memory = memory,
            Parallelism = parallelism
        };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_NewPayloadsOnly_WithMismatchedKdfSettings_ReturnsKdfValidationError(
        string masterPasswordHash)
    {
        var authKdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };
        var unlockKdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 650000 };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = authKdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = unlockKdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("must have the same KDF configuration", result[0].ErrorMessage);
    }

    [Theory]
    [BitAutoData]
    public void Validate_NewPayloadsOnly_WithMismatchedSalts_ReturnsSaltValidationError(
        string masterPasswordHash)
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt-auth"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt-unlock"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Equal("Invalid master password salt.", result[0].ErrorMessage);
    }

    [Theory]
    [BitAutoData]
    public void Validate_LegacyPayloadsOnly_NoErrors(string masterPasswordHash, string newHash, string key)
    {
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            NewMasterPasswordHash = newHash,
            Key = key
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_BothNewAndLegacyPayloads_ReturnsConflictError(
        string masterPasswordHash, string newHash, string key)
    {
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 600000
        };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            NewMasterPasswordHash = newHash,
            Key = key,
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("Cannot provide both", result[0].ErrorMessage);
    }

    [Theory]
    [BitAutoData]
    public void Validate_NeitherNewNorLegacyPayloads_ReturnsError(string masterPasswordHash)
    {
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("Must provide either", result[0].ErrorMessage);
    }

    [Theory]
    [BitAutoData]
    public void Validate_OnlyUnlockData_ReturnsError(string masterPasswordHash)
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("Must provide either", result[0].ErrorMessage);
    }

    [Theory]
    [BitAutoData]
    public void Validate_OnlyAuthenticationData_ReturnsError(string masterPasswordHash)
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("Must provide either", result[0].ErrorMessage);
    }

    [Theory]
    [BitAutoData]
    public void RequestHasNewDataTypes_WithBothPresent_ReturnsTrue(string masterPasswordHash)
    {
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 600000
        };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        Assert.True(model.RequestHasNewDataTypes());
    }

    [Theory]
    [BitAutoData]
    public void RequestHasNewDataTypes_WithLegacyOnly_ReturnsFalse(
        string masterPasswordHash, string newHash, string key)
    {
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            NewMasterPasswordHash = newHash,
            Key = key
        };

        Assert.False(model.RequestHasNewDataTypes());
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenBothAuthAndUnlockPresent_WithBelowMinimumKdf_NoError(
        string masterPasswordHash)
    {
        // Regression guard (PM-35306): legacy users with sub-minimum KDF settings must be able to
        // change their master password. KDF strength is enforced in the commands for registration
        // and KDF change, NOT in change-password.
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 1
        };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.DoesNotContain(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("KDF iterations must be between"));
    }
}
