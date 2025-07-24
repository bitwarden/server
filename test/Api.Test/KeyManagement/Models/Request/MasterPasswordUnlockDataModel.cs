#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Models.Request;

public class MasterPasswordUnlockDataModelTests
{

    readonly string _mockEncryptedString = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 5000, null, null)]
    [InlineData(KdfType.PBKDF2_SHA256, 100000, null, null)]
    [InlineData(KdfType.PBKDF2_SHA256, 600000, null, null)]
    [InlineData(KdfType.Argon2id, 3, 64, 4)]
    public void Validate_Success(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        var model = new MasterPasswordUnlockAndAuthenticationData
        {
            KdfType = kdfType,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            Email = "example@example.com",
            MasterKeyAuthenticationHash = "hash",
            MasterKeyEncryptedUserKey = _mockEncryptedString,
            MasterPasswordHint = "hint"
        };
        var result = Validate(model);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(KdfType.Argon2id, 1, null, 1)]
    [InlineData(KdfType.Argon2id, 1, 64, null)]
    [InlineData(KdfType.PBKDF2_SHA256, 5000, 0, null)]
    [InlineData(KdfType.PBKDF2_SHA256, 5000, null, 0)]
    [InlineData(KdfType.PBKDF2_SHA256, 5000, 0, 0)]
    [InlineData((KdfType)2, 100000, null, null)]
    [InlineData((KdfType)2, 2, 64, 4)]
    public void Validate_Failure(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        var model = new MasterPasswordUnlockAndAuthenticationData
        {
            KdfType = kdfType,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            Email = "example@example.com",
            MasterKeyAuthenticationHash = "hash",
            MasterKeyEncryptedUserKey = _mockEncryptedString,
            MasterPasswordHint = "hint"
        };
        var result = Validate(model);
        Assert.Single(result);
        Assert.NotNull(result.First().ErrorMessage);
    }

    private static List<ValidationResult> Validate(MasterPasswordUnlockAndAuthenticationData model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
