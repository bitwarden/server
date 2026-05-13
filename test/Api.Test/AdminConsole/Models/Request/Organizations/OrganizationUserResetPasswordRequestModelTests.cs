using System.ComponentModel.DataAnnotations;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationUserResetPasswordRequestModelTests
{
    [Theory]
    [BitAutoData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [BitAutoData(KdfType.Argon2id, 3, 64, 4)]
    public void Validate_UnlockAndAuthenticationDataOnly_NoErrors(
        KdfType kdfType, int iterations, int? memory, int? parallelism)
    {
        var kdf = new KdfRequestModel
        {
            KdfType = kdfType,
            Iterations = iterations,
            Memory = memory,
            Parallelism = parallelism
        };

        var model = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
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
    public void Validate_UnlockAndAuthenticationDataOnly_WithMismatchedKdfSettings_ReturnsKdfValidationError()
    {
        var authKdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };
        var unlockKdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 650000 };

        var model = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
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

    [Fact]
    public void Validate_UnlockAndAuthenticationDataOnly_WithMismatchedSalts_ReturnsSaltValidationError()
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
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

    [Fact]
    public void Validate_NoPayloadsProvided_ReturnsError()
    {
        var model = new OrganizationUserResetPasswordRequestModel { ResetMasterPassword = true };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("Must provide either", result[0].ErrorMessage);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public void Validate_OnlyOneOfUnlockAndAuthenticationData_ReturnsError(bool provideAuthenticationData)
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
            AuthenticationData = provideAuthenticationData
                ? new MasterPasswordAuthenticationDataRequestModel
                {
                    Kdf = kdf,
                    MasterPasswordAuthenticationHash = "authHash",
                    Salt = "salt"
                }
                : null,
            UnlockData = !provideAuthenticationData
                ? new MasterPasswordUnlockDataRequestModel
                {
                    Kdf = kdf,
                    MasterKeyWrappedUserKey = "wrappedKey",
                    Salt = "salt"
                }
                : null
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("Must provide either", result[0].ErrorMessage);
    }

    [Fact]
    public void Validate_TwoFactorOnlyReset_NoPayload_NoErrors()
    {
        var model = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = false,
            ResetTwoFactor = true
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_V1LegacyRequest_HashAndKey_NoErrors(string newHash, string key)
    {
        // v1 clients do not send ResetMasterPassword; it defaults to false.
        // Hash+key payload always present on v1 — validation should pass.
        var model = new OrganizationUserResetPasswordRequestModel
        {
            NewMasterPasswordHash = newHash,
            Key = key
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void RequestHasNewDataTypes_WithUnlockAndAuthenticationData_ReturnsTrue()
    {
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 600000
        };

        var model = new OrganizationUserResetPasswordRequestModel
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

        Assert.True(model.RequestHasNewDataTypes());
    }

    [Theory]
    [BitAutoData]
    public void RequestHasNewDataTypes_WithHashAndKeyOnly_ReturnsFalse(string newHash, string key)
    {
        var model = new OrganizationUserResetPasswordRequestModel
        {
            NewMasterPasswordHash = newHash,
            Key = key
        };

        Assert.False(model.RequestHasNewDataTypes());
    }

    [Fact]
    public void Validate_WhenBothAuthAndUnlockPresent_WithBelowMinimumKdf_NoError()
    {
        // Regression guard: legacy users with sub-minimum KDF settings must be able to have
        // their master password recovered by an admin. KDF strength is enforced upstream
        // (registration, KDF change), NOT at the account-recovery request model level.
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 1
        };

        var model = new OrganizationUserResetPasswordRequestModel
        {
            ResetMasterPassword = true,
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
