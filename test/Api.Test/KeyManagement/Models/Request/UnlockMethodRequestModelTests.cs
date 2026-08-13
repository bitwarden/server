using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Enums;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Models.Request;

public class UnlockMethodRequestModelTests
{
    private const string _wrappedUserKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private const string _salt = "mockSalt";

    [Fact]
    public void Validate_MasterPassword_ValidData_PassesValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.MasterPassword,
            MasterPasswordUnlockData = BuildMasterPasswordUnlockDataRequestModel(),
            KeyConnectorKeyWrappedUserKey = null
        };

        var result = Validate(model);
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_MasterPassword_MissingMasterPasswordUnlockData_FailsValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.MasterPassword,
            MasterPasswordUnlockData = null,
            KeyConnectorKeyWrappedUserKey = null
        };

        var result = Validate(model);
        Assert.Single(result);
        Assert.NotNull(result.First().ErrorMessage);
    }

    [Fact]
    public void Validate_MasterPassword_KeyConnectorKeyPresent_FailsValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.MasterPassword,
            MasterPasswordUnlockData = BuildMasterPasswordUnlockDataRequestModel(),
            KeyConnectorKeyWrappedUserKey = _wrappedUserKey
        };

        var result = Validate(model);
        Assert.Single(result);
        Assert.NotNull(result.First().ErrorMessage);
    }

    [Fact]
    public void Validate_Tde_NoExtraData_PassesValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.Tde,
            MasterPasswordUnlockData = null,
            KeyConnectorKeyWrappedUserKey = null
        };

        var result = Validate(model);
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_Tde_MasterPasswordUnlockDataPresent_FailsValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.Tde,
            MasterPasswordUnlockData = BuildMasterPasswordUnlockDataRequestModel(),
            KeyConnectorKeyWrappedUserKey = null
        };

        var result = Validate(model);
        Assert.Single(result);
        Assert.NotNull(result.First().ErrorMessage);
    }

    [Fact]
    public void Validate_Tde_KeyConnectorKeyPresent_FailsValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.Tde,
            MasterPasswordUnlockData = null,
            KeyConnectorKeyWrappedUserKey = _wrappedUserKey
        };

        var result = Validate(model);
        Assert.Single(result);
        Assert.NotNull(result.First().ErrorMessage);
    }

    [Fact]
    public void Validate_KeyConnector_ValidData_PassesValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.KeyConnector,
            MasterPasswordUnlockData = null,
            KeyConnectorKeyWrappedUserKey = _wrappedUserKey
        };

        var result = Validate(model);
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_KeyConnector_MissingKeyConnectorKey_FailsValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.KeyConnector,
            MasterPasswordUnlockData = null,
            KeyConnectorKeyWrappedUserKey = null
        };

        var result = Validate(model);
        Assert.Single(result);
        Assert.NotNull(result.First().ErrorMessage);
    }

    [Fact]
    public void Validate_KeyConnector_MasterPasswordUnlockDataPresent_FailsValidation()
    {
        var model = new UnlockMethodRequestModel
        {
            UnlockMethod = UnlockMethod.KeyConnector,
            MasterPasswordUnlockData = BuildMasterPasswordUnlockDataRequestModel(),
            KeyConnectorKeyWrappedUserKey = _wrappedUserKey
        };

        var result = Validate(model);
        Assert.Single(result);
        Assert.NotNull(result.First().ErrorMessage);
    }

    private static List<ValidationResult> Validate(UnlockMethodRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }

    private static MasterPasswordUnlockDataRequestModel BuildMasterPasswordUnlockDataRequestModel() => new()
    {
        Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
        MasterKeyWrappedUserKey = _wrappedUserKey,
        Salt = _salt
    };
}
