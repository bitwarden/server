using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationIntegrationConfigurationRequestModelTests
{
    [Fact]
    public void IsValidForType_CloudBillingSyncIntegration_ReturnsFalse()
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = "{}",
            Template = "template"
        };

        Assert.False(model.isValidForType(IntegrationType.CloudBillingSync));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void IsValidForType_EmptyConfiguration_ReturnsFalse(string? config)
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = "template"
        };

        var result = model.isValidForType(IntegrationType.Slack);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void IsValidForType_EmptyTemplate_ReturnsFalse(string? template)
    {
        var config = JsonSerializer.Serialize(new WebhookIntegrationConfiguration("https://example.com"));
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = template
        };

        Assert.False(model.isValidForType(IntegrationType.Webhook));
    }

    [Fact]
    public void IsValidForType_InvalidJsonConfiguration_ReturnsFalse()
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = "{not valid json}",
            Template = "template"
        };

        Assert.False(model.isValidForType(IntegrationType.Webhook));
    }

    [Fact]
    public void IsValidForType_ScimIntegration_ReturnsFalse()
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = "{}",
            Template = "template"
        };

        Assert.False(model.isValidForType(IntegrationType.Scim));
    }

    [Fact]
    public void IsValidForType_ValidSlackConfiguration_ReturnsTrue()
    {
        var config = JsonSerializer.Serialize(new SlackIntegrationConfiguration("C12345"));

        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = "template"
        };

        Assert.True(model.isValidForType(IntegrationType.Slack));
    }

    [Fact]
    public void IsValidForType_ValidWebhookConfiguration_ReturnsTrue()
    {
        var config = JsonSerializer.Serialize(new WebhookIntegrationConfiguration("https://example.com"));
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = "template"
        };

        Assert.True(model.isValidForType(IntegrationType.Webhook));
    }

    [Fact]
    public void IsValidForType_UnknownIntegrationType_ReturnsFalse()
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = "{}",
            Template = "template"
        };

        var unknownType = (IntegrationType)999;

        Assert.False(model.isValidForType(unknownType));
    }
}
