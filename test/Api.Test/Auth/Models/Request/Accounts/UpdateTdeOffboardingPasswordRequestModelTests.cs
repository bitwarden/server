using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request.Accounts;

public class UpdateTdeOffboardingPasswordRequestModelTests
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

        var model = new UpdateTdeOffboardingPasswordRequestModel
        {
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

    [Fact]
    public void Validate_NewPayloadsOnly_WithMismatchedKdfSettings_ReturnsKdfValidationError()
    {
        var authKdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };
        var unlockKdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 650000 };

        var model = new UpdateTdeOffboardingPasswordRequestModel
        {
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
    public void Validate_LegacyPayloadsOnly_NoErrors(string newHash, string key)
    {
        var model = new UpdateTdeOffboardingPasswordRequestModel
        {
            NewMasterPasswordHash = newHash,
            Key = key
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_BothNewAndLegacyPayloads_NoErrors(string newHash, string key)
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new UpdateTdeOffboardingPasswordRequestModel
        {
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

        Assert.Empty(result);
    }

    [Fact]
    public void Validate_NeitherNewNorLegacyPayloads_ReturnsError()
    {
        var model = new UpdateTdeOffboardingPasswordRequestModel();

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("Must provide either", result[0].ErrorMessage);
    }

    [Fact]
    public void Validate_OnlyUnlockData_ReturnsError()
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new UpdateTdeOffboardingPasswordRequestModel
        {
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

    [Fact]
    public void Validate_OnlyAuthenticationData_ReturnsError()
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new UpdateTdeOffboardingPasswordRequestModel
        {
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
    public void RequestHasNewDataTypes_WithBothPresent_ReturnsTrue(string hint)
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new UpdateTdeOffboardingPasswordRequestModel
        {
            MasterPasswordHint = hint,
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
    public void RequestHasNewDataTypes_WithLegacyOnly_ReturnsFalse(string newHash, string key)
    {
        var model = new UpdateTdeOffboardingPasswordRequestModel
        {
            NewMasterPasswordHash = newHash,
            Key = key
        };

        Assert.False(model.RequestHasNewDataTypes());
    }
}
