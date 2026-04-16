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

    private static List<ValidationResult> Validate(CreateOrganizationInviteLinkRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
