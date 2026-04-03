using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request.Accounts;

public class PasswordRequestModelTests
{
    #region Centralized Validation Tests

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
            UnlockData = new MasterPasswordUnlockDataRequestModel
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
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("KDF settings must be equal"));
    }

    [Fact]
    public void Validate_WhenBothAuthAndUnlockPresent_WithMismatchedSalt_ReturnsError()
    {
        // Arrange
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
                Salt = "salt2" // Different salt
            }
        };

        // Act
        var result = model.Validate(new ValidationContext(model)).ToList();

        // Assert
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("Salt must be equal"));
    }

    [Fact]
    public void Validate_WhenBothAuthAndUnlockPresent_WithInvalidKdf_ReturnsError()
    {
        // Arrange
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 1 // Too low
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
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("KDF iterations must be between"));
    }

    #endregion

    #region Both-or-Neither Tests

    /// <summary>
    /// This test proves backwards compatibility for clients that don't send either AuthenticationData or UnlockData
    /// </summary>
    [Fact]
    public void Validate_WhenBothNull_NoAuthUnlockErrors()
    {
        // Arrange
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

        // Assert — no auth/unlock-related errors
        Assert.Empty(result);
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

    #endregion

    #region Base Validation Preserved

    [Fact]
    public void Validate_BaseValidation_StillRuns()
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

        // Assert — base validation should require a secret
        Assert.Contains(result, r => r.ErrorMessage != null && r.ErrorMessage.Contains("must be supplied"));
    }

    #endregion
}
