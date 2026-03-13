using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Models.Requests;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Models.Request;

public class KeyConnectorEnrollmentRequestModelTests
{
    private const string _wrappedUserKey = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    [Fact]
    public void Validate_KeyConnectorKeyWrappedUserKeyNull_Invalid()
    {
        var model = new KeyConnectorEnrollmentRequestModel
        {
            KeyConnectorKeyWrappedUserKey = null!
        };

        var results = Validate(model);

        Assert.Contains(results,
            r => r.ErrorMessage == "KeyConnectorKeyWrappedUserKey must be supplied when request body is provided.");
    }

    [Fact]
    public void Validate_KeyConnectorKeyWrappedUserKeyWhitespace_Invalid()
    {
        var model = new KeyConnectorEnrollmentRequestModel
        {
            KeyConnectorKeyWrappedUserKey = " "
        };

        var results = Validate(model);

        Assert.Contains(results,
            r => r.ErrorMessage == "KeyConnectorKeyWrappedUserKey is not a valid encrypted string.");
    }

    [Fact]
    public void Validate_KeyConnectorKeyWrappedUserKeyValid_Success()
    {
        var model = new KeyConnectorEnrollmentRequestModel
        {
            KeyConnectorKeyWrappedUserKey = _wrappedUserKey
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    private static List<ValidationResult> Validate(KeyConnectorEnrollmentRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
