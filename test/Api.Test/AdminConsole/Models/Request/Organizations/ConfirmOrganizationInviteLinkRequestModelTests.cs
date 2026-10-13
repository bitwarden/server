using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class ConfirmOrganizationInviteLinkRequestModelTests
{
    private const string ValidEncryptedString = "4.YWJjZA==";

    [Fact]
    public void Validate_WithNullResetPasswordKey_ReturnsNoErrors()
    {
        var model = BuildModel(null);

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithValidResetPasswordKey_ReturnsNoErrors()
    {
        var model = BuildModel(ValidEncryptedString);

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    public void Validate_WithInvalidResetPasswordKey_ReturnsError(string resetPasswordKey)
    {
        var model = BuildModel(resetPasswordKey);

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.ResetPasswordKey)));
    }

    private static ConfirmOrganizationInviteLinkRequestModel BuildModel(string? resetPasswordKey) =>
        new()
        {
            OrganizationId = Guid.NewGuid(),
            Code = Guid.NewGuid(),
            OrgUserKey = "org-user-key",
            DefaultUserCollectionName = ValidEncryptedString,
            ResetPasswordKey = resetPasswordKey,
        };

    private static List<ValidationResult> Validate(ConfirmOrganizationInviteLinkRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
