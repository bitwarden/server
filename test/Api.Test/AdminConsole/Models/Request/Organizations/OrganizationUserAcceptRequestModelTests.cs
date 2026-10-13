using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationUserAcceptRequestModelTests
{
    private const string ValidResetPasswordKey = "4.YWJjZA==";

    [Fact]
    public void Validate_WithNullResetPasswordKey_ReturnsNoErrors()
    {
        var model = new OrganizationUserAcceptRequestModel
        {
            Token = "token",
            ResetPasswordKey = null,
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithValidResetPasswordKey_ReturnsNoErrors()
    {
        var model = new OrganizationUserAcceptRequestModel
        {
            Token = "token",
            ResetPasswordKey = ValidResetPasswordKey,
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    public void Validate_WithInvalidResetPasswordKey_ReturnsError(string resetPasswordKey)
    {
        var model = new OrganizationUserAcceptRequestModel
        {
            Token = "token",
            ResetPasswordKey = resetPasswordKey,
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.ResetPasswordKey)));
    }

    private static List<ValidationResult> Validate(OrganizationUserAcceptRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
