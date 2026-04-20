using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request.Accounts;

public class PasswordRequestModelTests
{
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [InlineData(KdfType.Argon2id, 3, 64, 4)]
    public void Validate_WhenBothAuthAndUnlockPresent_WithMatchingKdf_NoAuthUnlockErrors(
        KdfType kdfType, int iterations, int? memory, int? parallelism)
    {
        // Arrange
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
            NewMasterPasswordHash = "newHash",
            Key = "key",
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

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_WhenBothAuthAndUnlockPresent_WithMismatchedKdf_ReturnsError()
    {
        // Request model enforces matching KDF settings between AuthenticationData and UnlockData.
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key",
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 650000
                },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Contains(result, r => r.ErrorMessage == "AuthenticationData and UnlockData must have the same KDF configuration.");
    }

    [Fact]
    public void Validate_WhenBothAuthAndUnlockPresent_WithMismatchedSalt_ReturnsError()
    {
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 600000
        };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key",
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt1"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt2"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Contains(result, r => r.ErrorMessage == "AuthenticationData and UnlockData must have the same salt.");
    }

    [Fact]
    public void Validate_WhenBothAuthAndUnlockPresent_WithMismatchedKdfAndSalt_ReturnsBothErrors()
    {
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key",
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt1"
            },
            UnlockData = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.Argon2id,
                    Iterations = 3,
                    Memory = 64,
                    Parallelism = 4
                },
                MasterKeyWrappedUserKey = "wrappedKey",
                Salt = "salt2"
            }
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Contains(result, r => r.ErrorMessage == "AuthenticationData and UnlockData must have the same KDF configuration.");
        Assert.Contains(result, r => r.ErrorMessage == "AuthenticationData and UnlockData must have the same salt.");
    }

    [Fact]
    public void Validate_WhenBothAuthAndUnlockPresent_WithBelowMinimumKdf_NoError()
    {
        // Regression guard: legacy users with sub-minimum KDF settings must be able to change
        // their master password. KDF strength is enforced only on /accounts/kdf (ChangeKdfCommand).
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 1
        };

        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key",
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

    [Fact]
    public void Validate_WhenOnlyAuthPresent_ReturnsError()
    {
        // Arrange
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key",
            AuthenticationData = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 600000
                },
                MasterPasswordAuthenticationHash = "authHash",
                Salt = "salt"
            },
            UnlockData = null
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains(nameof(PasswordRequestModel.UnlockData)));
    }

    [Fact]
    public void Validate_WhenOnlyUnlockPresent_ReturnsError()
    {
        // Arrange
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key",
            AuthenticationData = null,
            UnlockData = new MasterPasswordUnlockDataRequestModel
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
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains(nameof(PasswordRequestModel.AuthenticationData)));
    }

    [Fact]
    public void Validate_WhenNeitherAuthNorUnlockPresent_NoAuthUnlockErrors()
    {
        // Arrange — backward compat: old clients send neither field
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key",
            AuthenticationData = null,
            UnlockData = null
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert — no errors about AuthenticationData or UnlockData
        Assert.DoesNotContain(result, r =>
            r.ErrorMessage != null &&
            (r.ErrorMessage.Contains(nameof(PasswordRequestModel.AuthenticationData)) ||
             r.ErrorMessage.Contains(nameof(PasswordRequestModel.UnlockData))));
    }

    [Fact]
    public void Validate_LegacyValidationFailsFirst()
    {
        // Arrange — no MasterPasswordHash, OTP, or AuthRequestAccessCode
        var model = new PasswordRequestModel
        {
            NewMasterPasswordHash = "newHash",
            Key = "key",
            AuthenticationData = null,
            UnlockData = null
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert — legacy validation should fail first
        Assert.Contains(result,
        r => r.ErrorMessage != null &&
        r.ErrorMessage.Contains(nameof(PasswordRequestModel.MasterPasswordHash)) &&
        // NOT auth/unlock errors
        !r.ErrorMessage.Contains(nameof(PasswordRequestModel.AuthenticationData)) &&
        !r.ErrorMessage.Contains(nameof(PasswordRequestModel.UnlockData)));

    }
}
