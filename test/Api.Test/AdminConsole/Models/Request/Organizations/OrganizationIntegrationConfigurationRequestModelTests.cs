using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;
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

        Assert.False(condition: model.IsValidForType(IntegrationType.CloudBillingSync));
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "    ")]
    public void IsValidForType_EmptyConfiguration_ReturnsFalse(string? config)
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = "template"
        };

        Assert.False(condition: model.IsValidForType(IntegrationType.Slack));
        Assert.False(condition: model.IsValidForType(IntegrationType.Webhook));
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: "    ")]
    public void IsValidForType_EmptyNonNullConfiguration_ReturnsFalse(string? config)
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = "template"
        };

        Assert.False(condition: model.IsValidForType(IntegrationType.Hec));
        Assert.False(condition: model.IsValidForType(IntegrationType.Datadog));
        Assert.False(condition: model.IsValidForType(IntegrationType.Teams));
    }

    [Fact]
    public void IsValidForType_NullConfiguration_ReturnsTrue()
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = null,
            Template = "template"
        };

        Assert.True(condition: model.IsValidForType(IntegrationType.Hec));
        Assert.True(condition: model.IsValidForType(IntegrationType.Datadog));
        Assert.True(condition: model.IsValidForType(IntegrationType.Teams));
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "    ")]
    public void IsValidForType_EmptyTemplate_ReturnsFalse(string? template)
    {
        var config = JsonSerializer.Serialize(value: new WebhookIntegrationConfiguration(
            Uri: new Uri("https://localhost"),
            Scheme: "Bearer",
            Token: "AUTH-TOKEN"));
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = template
        };

        Assert.False(condition: model.IsValidForType(IntegrationType.Slack));
        Assert.False(condition: model.IsValidForType(IntegrationType.Webhook));
        Assert.False(condition: model.IsValidForType(IntegrationType.Hec));
        Assert.False(condition: model.IsValidForType(IntegrationType.Datadog));
        Assert.False(condition: model.IsValidForType(IntegrationType.Teams));
    }

    [Fact]
    public void IsValidForType_InvalidJsonConfiguration_ReturnsFalse()
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = "{not valid json}",
            Template = "template"
        };

        Assert.False(condition: model.IsValidForType(IntegrationType.Slack));
        Assert.False(condition: model.IsValidForType(IntegrationType.Webhook));
        Assert.False(condition: model.IsValidForType(IntegrationType.Hec));
        Assert.False(condition: model.IsValidForType(IntegrationType.Datadog));
        Assert.False(condition: model.IsValidForType(IntegrationType.Teams));
    }


    [Fact]
    public void IsValidForType_InvalidJsonFilters_ReturnsFalse()
    {
        var config = JsonSerializer.Serialize(new WebhookIntegrationConfiguration(Uri: new Uri("https://example.com")));
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Filters = "{Not valid json",
            Template = "template"
        };

        Assert.False(model.IsValidForType(IntegrationType.Webhook));
    }

    [Fact]
    public void IsValidForType_ScimIntegration_ReturnsFalse()
    {
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = "{}",
            Template = "template"
        };

        Assert.False(condition: model.IsValidForType(IntegrationType.Scim));
    }

    [Fact]
    public void IsValidForType_ValidSlackConfiguration_ReturnsTrue()
    {
        var config = JsonSerializer.Serialize(value: new SlackIntegrationConfiguration(ChannelId: "C12345"));

        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = "template"
        };

        Assert.True(condition: model.IsValidForType(IntegrationType.Slack));
    }

    [Fact]
    public void IsValidForType_ValidSlackConfigurationWithFilters_ReturnsTrue()
    {
        var config = JsonSerializer.Serialize(new SlackIntegrationConfiguration("C12345"));
        var filters = JsonSerializer.Serialize(new IntegrationFilterGroup()
        {
            AndOperator = true,
            Rules = [
                new IntegrationFilterRule()
                {
                    Operation = IntegrationFilterOperation.Equals,
                    Property = "CollectionId",
                    Value = Guid.NewGuid()
                }
            ],
            Groups = []
        });
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Filters = filters,
            Template = "template"
        };

        Assert.True(model.IsValidForType(IntegrationType.Slack));
    }

    [Fact]
    public void IsValidForType_ValidNoAuthWebhookConfiguration_ReturnsTrue()
    {
        var config = JsonSerializer.Serialize(value: new WebhookIntegrationConfiguration(Uri: new Uri("https://localhost")));
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = "template"
        };

        Assert.True(condition: model.IsValidForType(IntegrationType.Webhook));
    }

    [Fact]
    public void IsValidForType_ValidWebhookConfiguration_ReturnsTrue()
    {
        var config = JsonSerializer.Serialize(value: new WebhookIntegrationConfiguration(
            Uri: new Uri("https://localhost"),
            Scheme: "Bearer",
            Token: "AUTH-TOKEN"));
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Template = "template"
        };

        Assert.True(condition: model.IsValidForType(IntegrationType.Webhook));
    }

    [Fact]
    public void IsValidForType_ValidWebhookConfigurationWithFilters_ReturnsTrue()
    {
        var config = JsonSerializer.Serialize(new WebhookIntegrationConfiguration(
            Uri: new Uri("https://example.com"),
            Scheme: "Bearer",
            Token: "AUTH-TOKEN"));
        var filters = JsonSerializer.Serialize(new IntegrationFilterGroup()
        {
            AndOperator = true,
            Rules = [
                new IntegrationFilterRule()
                {
                    Operation = IntegrationFilterOperation.Equals,
                    Property = "CollectionId",
                    Value = Guid.NewGuid()
                }
            ],
            Groups = []
        });
        var model = new OrganizationIntegrationConfigurationRequestModel
        {
            Configuration = config,
            Filters = filters,
            Template = "template"
        };

        Assert.True(model.IsValidForType(IntegrationType.Webhook));
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

        Assert.False(condition: model.IsValidForType(unknownType));
    }
}
