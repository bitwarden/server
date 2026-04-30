using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class CreateOrganizationInviteLinkRequestModelTests
{
    private const string _validEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    [Fact]
    public void Validate_ValidModel_ReturnsNoErrors()
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = new[] { "acme.com" },
            EncryptedInviteKey = _validEncryptedString,
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("not a domain")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("double..dot.com")]
    [InlineData("-starts-with-hyphen.com")]
    [InlineData(" acme.com ")]
    public void Validate_WithInvalidDomainFormat_ReturnsError(string invalidDomain)
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = new[] { invalidDomain },
            EncryptedInviteKey = _validEncryptedString,
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.AllowedDomains)));
    }

    [Fact]
    public void Validate_WithEmptyAllowedDomains_ReturnsError()
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = Array.Empty<string>(),
            EncryptedInviteKey = _validEncryptedString,
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.AllowedDomains)));
    }

    [Fact]
    public void Validate_WithMixedValidAndInvalidDomains_ReturnsError()
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = new[] { "acme.com", "not a domain", "<script>" },
            EncryptedInviteKey = _validEncryptedString,
        };

        var results = Validate(model);

        var error = Assert.Single(results);
        Assert.Contains("'not a domain'", error.ErrorMessage);
        Assert.Contains("'<script>'", error.ErrorMessage);
    }

    private static List<ValidationResult> Validate(CreateOrganizationInviteLinkRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
