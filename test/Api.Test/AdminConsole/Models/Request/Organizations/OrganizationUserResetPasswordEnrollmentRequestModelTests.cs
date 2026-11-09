using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationUserResetPasswordEnrollmentRequestModelTests
{
    private const string ValidResetPasswordKey = "4.YWJjZA==";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankResetPasswordKey_ReturnsNoErrors(string? resetPasswordKey)
    {
        var model = new OrganizationUserResetPasswordEnrollmentRequestModel
        {
            ResetPasswordKey = resetPasswordKey,
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithValidResetPasswordKey_ReturnsNoErrors()
    {
        var model = new OrganizationUserResetPasswordEnrollmentRequestModel
        {
            ResetPasswordKey = ValidResetPasswordKey,
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithResetPasswordKeyOverMaximumLength_ReturnsError()
    {
        var model = new OrganizationUserResetPasswordEnrollmentRequestModel
        {
            ResetPasswordKey = "4." + new string('A', 1000),
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.ResetPasswordKey)));
    }

    private static List<ValidationResult> Validate(OrganizationUserResetPasswordEnrollmentRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
