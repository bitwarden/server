using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class UpdateOrganizationInviteLinkRequestModelTests
{
    [Fact]
    public void Validate_WithValidModel_ReturnsNoErrors()
    {
        var model = new UpdateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
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
        var model = new UpdateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = [invalidDomain],
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.AllowedDomains)));
    }

    [Fact]
    public void Validate_WithEmptyAllowedDomains_ReturnsError()
    {
        var model = new UpdateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = [],
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.AllowedDomains)));
    }

    [Fact]
    public void Validate_WithMixedValidAndInvalidDomains_ReturnsError()
    {
        var model = new UpdateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com", "not a domain", "<script>"],
        };

        var results = Validate(model);

        var error = Assert.Single(results);
        Assert.Contains("'not a domain'", error.ErrorMessage);
        Assert.Contains("'<script>'", error.ErrorMessage);
    }

    private static List<ValidationResult> Validate(UpdateOrganizationInviteLinkRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
