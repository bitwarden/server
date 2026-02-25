using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationUserResetPasswordV2RequestModelTests
{
    private static readonly string MockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    [Fact]
    public void Validate_ValidModel_ReturnsNoErrors()
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };
        var model = new OrganizationUserResetPasswordV2RequestModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "hash",
                Salt = "user@example.com"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = MockEncryptedString,
                Salt = "user@example.com"
            }
        };

        var results = Validate(model);
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_KdfMismatch_ReturnsError()
    {
        var model = new OrganizationUserResetPasswordV2RequestModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
                MasterPasswordAuthenticationHash = "hash",
                Salt = "user@example.com"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = new KdfRequestModel { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 },
                MasterKeyWrappedUserKey = MockEncryptedString,
                Salt = "user@example.com"
            }
        };

        var results = Validate(model);
        Assert.Single(results);
        Assert.Contains("KDF settings must be equal", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_SaltMismatch_ReturnsError()
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 };
        var model = new OrganizationUserResetPasswordV2RequestModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "hash",
                Salt = "user@example.com"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = MockEncryptedString,
                Salt = "different-user@example.com"
            }
        };

        var results = Validate(model);
        Assert.Single(results);
        Assert.Contains("Salt must be equal", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_InvalidKdfRange_ReturnsError()
    {
        // Iterations below PBKDF2 minimum
        var kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 100 };
        var model = new OrganizationUserResetPasswordV2RequestModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "hash",
                Salt = "user@example.com"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = MockEncryptedString,
                Salt = "user@example.com"
            }
        };

        var results = Validate(model);
        Assert.Single(results);
        Assert.Contains("KDF iterations must be between", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Argon2id_ValidSettings_ReturnsNoErrors()
    {
        var kdf = new KdfRequestModel { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 };
        var model = new OrganizationUserResetPasswordV2RequestModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = "hash",
                Salt = "user@example.com"
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = MockEncryptedString,
                Salt = "user@example.com"
            }
        };

        var results = Validate(model);
        Assert.Empty(results);
    }

    private static List<ValidationResult> Validate(OrganizationUserResetPasswordV2RequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
