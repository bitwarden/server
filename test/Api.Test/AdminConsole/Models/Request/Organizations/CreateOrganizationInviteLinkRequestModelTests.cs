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
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = _validEncryptedString,
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithValidEncryptedOrgKey_ReturnsNoErrors()
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = _validEncryptedString,
            EncryptedOrgKey = _validEncryptedString,
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_EncryptedInviteKeyNotEncryptedString_ReturnsError()
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = "not-an-encrypted-string",
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "EncryptedInviteKey is not a valid encrypted string.");
    }

    [Fact]
    public void Validate_EncryptedOrgKeyNotEncryptedString_ReturnsError()
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = _validEncryptedString,
            EncryptedOrgKey = "not-an-encrypted-string",
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage == "EncryptedOrgKey is not a valid encrypted string.");
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
            AllowedDomains = [invalidDomain],
            EncryptedInviteKey = _validEncryptedString,
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.AllowedDomains)));
    }

    [Fact]
    public void Validate_WithMixedValidAndInvalidDomains_ReturnsOneErrorPerInvalidDomain()
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com", "not a domain", "<script>"],
            EncryptedInviteKey = _validEncryptedString,
        };

        var results = Validate(model);

        Assert.Equal(2, results.Count);
    }

    private static List<ValidationResult> Validate(CreateOrganizationInviteLinkRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
