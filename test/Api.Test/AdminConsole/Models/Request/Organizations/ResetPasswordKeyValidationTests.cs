using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class ResetPasswordKeyValidationTests
{
    private const string ValidResetPasswordKey = "4.YWJjZA==";

    [Theory]
    [InlineData(null)]
    [InlineData(ValidResetPasswordKey)]
    public void Validate_AcceptModel_WhenKeyIsAbsentOrWellFormed_IsValid(string? resetPasswordKey)
    {
        var model = new OrganizationUserAcceptRequestModel
        {
            Token = "token",
            ResetPasswordKey = resetPasswordKey
        };

        Assert.Empty(Validate(model));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("not-a-key")]
    [InlineData("2.enc-key")]
    public void Validate_AcceptModel_WhenKeyIsNotAnEncryptedString_IsInvalid(string resetPasswordKey)
    {
        var model = new OrganizationUserAcceptRequestModel
        {
            Token = "token",
            ResetPasswordKey = resetPasswordKey
        };

        Assert.Single(Validate(model));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("not-a-key")]
    public void Validate_AcceptInviteLinkModel_WhenKeyIsNotAnEncryptedString_IsInvalid(string resetPasswordKey)
    {
        var model = new AcceptOrganizationInviteLinkRequestModel
        {
            OrganizationId = Guid.NewGuid(),
            Code = Guid.NewGuid(),
            ResetPasswordKey = resetPasswordKey
        };

        Assert.Single(Validate(model));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("not-a-key")]
    public void Validate_ConfirmInviteLinkModel_WhenKeyIsNotAnEncryptedString_IsInvalid(string resetPasswordKey)
    {
        var model = new ConfirmOrganizationInviteLinkRequestModel
        {
            OrganizationId = Guid.NewGuid(),
            Code = Guid.NewGuid(),
            OrgUserKey = "org-user-key",
            DefaultUserCollectionName = ValidResetPasswordKey,
            ResetPasswordKey = resetPasswordKey
        };

        Assert.Single(Validate(model));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(ValidResetPasswordKey)]
    public void Validate_EnrollmentModel_WhenKeyIsBlankOrWellFormed_IsValid(string? resetPasswordKey)
    {
        var model = new OrganizationUserResetPasswordEnrollmentRequestModel
        {
            ResetPasswordKey = resetPasswordKey
        };

        Assert.Empty(Validate(model));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
