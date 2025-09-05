using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
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
    public void Validate_Webhook_WithInvalidConfiguration_ReturnsConfigurationError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Webhook,
            Configuration = "something"
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Configuration), results[0].MemberNames);
        Assert.Contains("must include valid configuration", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Webhook_WithValidConfiguration_ReturnsNoErrors()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Webhook,
            Configuration = JsonSerializer.Serialize(new WebhookIntegration(new Uri("https://example.com")))
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_Hec_WithNullConfiguration_ReturnsError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Hec,
            Configuration = null
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Configuration), results[0].MemberNames);
        Assert.Contains("must include valid configuration", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Hec_WithInvalidConfiguration_ReturnsError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Hec,
            Configuration = "Not valid"
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Configuration), results[0].MemberNames);
        Assert.Contains("must include valid configuration", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Hec_WithValidConfiguration_ReturnsNoErrors()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Hec,
            Configuration = JsonSerializer.Serialize(new HecIntegration(Uri: new Uri("http://localhost"), Scheme: "Bearer", Token: "Token"))
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_Datadog_WithNullConfiguration_ReturnsError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Datadog,
            Configuration = null
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Configuration), results[0].MemberNames);
        Assert.Contains("must include valid configuration", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Datadog_WithInvalidConfiguration_ReturnsError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Datadog,
            Configuration = "Not valid"
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Single(results);
        Assert.Contains(nameof(model.Configuration), results[0].MemberNames);
        Assert.Contains("must include valid configuration", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Datadog_WithValidConfiguration_ReturnsNoErrors()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Datadog,
            Configuration = JsonSerializer.Serialize(
                new DatadogIntegration(ApiKey: "API1234", Uri: new Uri("http://localhost"))
            )
        };

        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Empty(results);
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
