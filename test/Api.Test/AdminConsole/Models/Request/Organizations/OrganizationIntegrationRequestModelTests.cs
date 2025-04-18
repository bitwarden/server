using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationIntegrationRequestModelTests
{
    [Fact]
    public void Validate_CloudBillingSync_ReturnsNotYetSupportedError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.CloudBillingSync,
            Configuration = null
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Type), results[0].MemberNames);
        Assert.Contains("not yet supported", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Scim_ReturnsNotYetSupportedError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Scim,
            Configuration = null
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Type), results[0].MemberNames);
        Assert.Contains("not yet supported", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Slack_ReturnsCannotBeCreatedDirectlyError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Slack,
            Configuration = null
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Type), results[0].MemberNames);
        Assert.Contains("cannot be created directly", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Webhook_WithNullConfiguration_ReturnsNoErrors()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Webhook,
            Configuration = null
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_Webhook_WithConfiguration_ReturnsConfigurationError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Webhook,
            Configuration = "something"
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Configuration), results[0].MemberNames);
        Assert.Contains("must not include configuration", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_UnknownIntegrationType_ReturnsUnrecognizedError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = (IntegrationType)999,
            Configuration = null
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Type), results[0].MemberNames);
        Assert.Contains("not recognized", results[0].ErrorMessage);
    }
}
