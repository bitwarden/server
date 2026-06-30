using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class CreateOrganizationInviteLinkRequestModelTests
{
    private const string _invite = "opaque-invite-blob";

    [Fact]
    public void Validate_ValidModel_ReturnsNoErrors()
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = new[] { "acme.com" },
            Invite = _invite,
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
            Invite = _invite,
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
            Invite = _invite,
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
            Invite = _invite,
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
