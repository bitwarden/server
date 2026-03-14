using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class KdfSettingsValidatorTests
{
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 1_000_000, null, null)] // Somewhere in the middle
    [InlineData(KdfType.PBKDF2_SHA256, 600_000, null, null)] // Right on the lower boundary
    [InlineData(KdfType.PBKDF2_SHA256, 2_000_000, null, null)] // Right on the upper boundary
    [InlineData(KdfType.Argon2id, 5, 500, 8)] // Somewhere in the middle
    [InlineData(KdfType.Argon2id, 2, 15, 1)] // Right on the lower boundary
    [InlineData(KdfType.Argon2id, 10, 1024, 16)] // Right on the upper boundary
    public void Validate_IsValid(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        var results = KdfSettingsValidator.Validate(kdfType, kdfIterations, kdfMemory, kdfParallelism);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 500_000, null, null, 1)] // Too few iterations
    [InlineData(KdfType.PBKDF2_SHA256, 2_000_001, null, null, 1)] // Too many iterations
    [InlineData(KdfType.Argon2id, 0, 30, 8, 1)] // Iterations must be greater than 0
    [InlineData(KdfType.Argon2id, 10, 14, 8, 1)] // Too little memory
    [InlineData(KdfType.Argon2id, 10, 14, 0, 1)] // Too small of a parallelism value
    [InlineData(KdfType.Argon2id, 10, 1025, 8, 1)] // Too much memory
    [InlineData(KdfType.Argon2id, 10, 512, 17, 1)] // Too big of a parallelism value
    public void Validate_Fails(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism, int expectedFailures)
    {
        var results = KdfSettingsValidator.Validate(kdfType, kdfIterations, kdfMemory, kdfParallelism);
        Assert.NotEmpty(results);
        Assert.Equal(expectedFailures, results.Count());
    }

    [Fact]
    public void ValidateAuthenticationAndUnlockData_WhenMatchingKdfAndSalt_ReturnsNoErrors()
    {
        var kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 };
        var authentication = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "hash",
            Salt = "salt"
        };
        var unlock = new MasterPasswordUnlockData
        {
            Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 },
            MasterKeyWrappedUserKey = "wrapped",
            Salt = "salt"
        };

        var results = KdfSettingsValidator.ValidateAuthenticationAndUnlockData(authentication, unlock).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void ValidateAuthenticationAndUnlockData_WhenKdfMismatch_ReturnsKdfEqualityError()
    {
        var authentication = new MasterPasswordAuthenticationData
        {
            Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 },
            MasterPasswordAuthenticationHash = "hash",
            Salt = "salt"
        };
        var unlock = new MasterPasswordUnlockData
        {
            Kdf = new KdfSettings { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 },
            MasterKeyWrappedUserKey = "wrapped",
            Salt = "salt"
        };

        var results = KdfSettingsValidator.ValidateAuthenticationAndUnlockData(authentication, unlock).ToList();

        Assert.Single(results);
        Assert.Equal("KDF settings must be equal for authentication and unlock.", results[0].ErrorMessage);
    }

    [Fact]
    public void ValidateAuthenticationAndUnlockData_WhenSaltMismatch_ReturnsSaltEqualityError()
    {
        var authentication = new MasterPasswordAuthenticationData
        {
            Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 },
            MasterPasswordAuthenticationHash = "hash",
            Salt = "salt-auth"
        };
        var unlock = new MasterPasswordUnlockData
        {
            Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 },
            MasterKeyWrappedUserKey = "wrapped",
            Salt = "salt-unlock"
        };

        var results = KdfSettingsValidator.ValidateAuthenticationAndUnlockData(authentication, unlock).ToList();

        Assert.Single(results);
        Assert.Equal("Salt must be equal for authentication and unlock.", results[0].ErrorMessage);
    }

    [Fact]
    public void ValidateAuthenticationAndUnlockData_WhenKdfInvalid_ReturnsKdfValidationError()
    {
        // Matching but out-of-range KDF (iterations below minimum for PBKDF2)
        var authentication = new MasterPasswordAuthenticationData
        {
            Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 100 },
            MasterPasswordAuthenticationHash = "hash",
            Salt = "salt"
        };
        var unlock = new MasterPasswordUnlockData
        {
            Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 100 },
            MasterKeyWrappedUserKey = "wrapped",
            Salt = "salt"
        };

        var results = KdfSettingsValidator.ValidateAuthenticationAndUnlockData(authentication, unlock).ToList();

        Assert.Single(results);
        Assert.Contains("KDF iterations must be between", results[0].ErrorMessage);
    }
}
