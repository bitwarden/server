using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request.Accounts;

public class ChangeKdfRequestModelTests
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

        var model = new ChangeKdfRequestModel
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
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    [BitAutoData(true, true)]
    public void Validate_EitherFieldNull_ReturnsNoErrors(bool authNull, bool unlockNull,
        string masterPasswordHash)
    {
        // Defensive guard: [Required] reports null-field errors via ModelState, so Validate()
        // must not throw or report when either cross-field input is null.
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };

        var model = new ChangeKdfRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            AuthenticationData = authNull
                ? null
                : new MasterPasswordAuthenticationDataRequestModel
                {
                    Kdf = kdf,
                    MasterPasswordAuthenticationHash = "authHash",
                    Salt = "salt"
                },
            UnlockData = unlockNull
                ? null
                : new MasterPasswordUnlockDataRequestModel
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

        var model = new ChangeKdfRequestModel
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
}
