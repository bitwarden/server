using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Models.Request;

public class UnlockMethodRequestModelTests
{
    private const string _wrappedUserKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private const string _salt = "mockSalt";

    [Theory]
    [BitAutoData(true, true)]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    public void UnlockMethodRequestModel_ExpectedUnlockMethods_PassValidation(bool masterPasswordUnlockDataNull,
        bool keyConnectorNull)
    {
        var model = new UnlockMethodRequestModel
        {
            MasterPasswordUnlockData =
                masterPasswordUnlockDataNull ? null : BuildMasterPasswordUnlockDataRequestModel(),
            KeyConnectorKeyWrappedUserKey = keyConnectorNull ? null : _wrappedUserKey
        };

        var result = Validate(model);
        Assert.Empty(result);
    }

    [Fact]
    public void UnlockMethodRequestModel_BadData_ValidationFailure()
    {
        var model = new UnlockMethodRequestModel
        {
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

    private MasterPasswordUnlockDataRequestModel BuildMasterPasswordUnlockDataRequestModel() => new()
    {
        Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 60000 },
        MasterKeyWrappedUserKey = _wrappedUserKey,
        Salt = _salt
    };
}
