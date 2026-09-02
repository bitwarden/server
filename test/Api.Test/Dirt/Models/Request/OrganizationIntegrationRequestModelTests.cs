using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Api.Dirt.Models.Request;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Dirt.Models.Request;

public class OrganizationIntegrationRequestModelTests
{
    [Fact]
    public void ToOrganizationIntegration_CreatesNewOrganizationIntegration()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Hec,
            Configuration = JsonSerializer.Serialize(new HecIntegration(Uri: new Uri("http://localhost"), Scheme: "Bearer", Token: "Token"))
        };

        var organizationId = Guid.NewGuid();
        var organizationIntegration = model.ToOrganizationIntegration(organizationId);

        Assert.Equal(organizationIntegration.Type, model.Type);
        Assert.Equal(organizationIntegration.Configuration, model.Configuration);
        Assert.Equal(organizationIntegration.OrganizationId, organizationId);
    }

    [Theory, BitAutoData]
    public void ToOrganizationIntegration_UpdatesExistingOrganizationIntegration(OrganizationIntegration integration)
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Hec,
            Configuration = JsonSerializer.Serialize(new HecIntegration(Uri: new Uri("http://localhost"), Scheme: "Bearer", Token: "Token"))
        };

        var organizationIntegration = model.ToOrganizationIntegration(integration);

        Assert.Equal(organizationIntegration.Configuration, model.Configuration);
    }

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
    public void Validate_Teams_ReturnsCannotBeCreatedDirectlyError()
    {
        var model = new OrganizationIntegrationRequestModel
        {
            Type = IntegrationType.Teams,
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
        Assert.Contains("Must include valid", results[0].ErrorMessage);
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
        Assert.Contains("Must include valid", results[0].ErrorMessage);
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
        Assert.Contains("Must include valid", results[0].ErrorMessage);
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
        Assert.Contains("Must include valid", results[0].ErrorMessage);
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
        Assert.Contains("Must include valid", results[0].ErrorMessage);
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
