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

    [Fact]
    public void Validate_LegacyPayloadsOnly_NoErrors()
    {
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key"
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void Validate_BothNewAndLegacyPayloads_ReturnsError()
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

    [Fact]
    public void Validate_NeitherNewNorLegacyPayloads_ReturnsError()
    {
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash"
        };

        var result = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(result);
        Assert.Contains("Must provide either", result[0].ErrorMessage);
    }

    [Fact]
    public void RequestHasNewDataTypes_WithBothPresent_ReturnsTrue()
    {
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = 600000
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

        Assert.True(model.RequestHasNewDataTypes());
    }

    [Fact]
    public void RequestHasNewDataTypes_WithLegacyOnly_ReturnsFalse()
    {
        var model = new PasswordRequestModel
        {
            MasterPasswordHash = "masterPasswordHash",
            NewMasterPasswordHash = "newHash",
            Key = "key"
        };

        Assert.False(model.RequestHasNewDataTypes());
    }
}
