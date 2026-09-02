using System.Text.Json;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Services.Implementations;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

public class OrganizationIntegrationConfigurationValidatorTests
{
    private readonly OrganizationIntegrationConfigurationValidator _sut = new();

    [Fact]
    public void ValidateConfiguration_CloudBillingSyncIntegration_ReturnsFalse()
    {
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = "{}",
            Template = "template"
        };

        Assert.False(_sut.ValidateConfiguration(IntegrationType.CloudBillingSync, configuration));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void ValidateConfiguration_EmptyTemplate_ReturnsFalse(string? template)
    {
        var config1 = new OrganizationIntegrationConfiguration
        {
            Configuration = JsonSerializer.Serialize(new SlackIntegrationConfiguration(ChannelId: "C12345")),
            Template = template
        };
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Slack, config1));

        var config2 = new OrganizationIntegrationConfiguration
        {
            Configuration = JsonSerializer.Serialize(new WebhookIntegrationConfiguration(Uri: new Uri("https://example.com"))),
            Template = template
        };
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Webhook, config2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void ValidateConfiguration_EmptyNonNullConfiguration_ReturnsFalse(string? config)
    {
        var config1 = new OrganizationIntegrationConfiguration
        {
            Configuration = config,
            Template = "template"
        };
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Hec, config1));

        var config2 = new OrganizationIntegrationConfiguration
        {
            Configuration = config,
            Template = "template"
        };
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Datadog, config2));

        var config3 = new OrganizationIntegrationConfiguration
        {
            Configuration = config,
            Template = "template"
        };
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Teams, config3));
    }

    [Fact]
    public void ValidateConfiguration_NullConfiguration_ReturnsTrue()
    {
        var config1 = new OrganizationIntegrationConfiguration
        {
            Configuration = null,
            Template = "template"
        };
        Assert.True(_sut.ValidateConfiguration(IntegrationType.Hec, config1));

        var config2 = new OrganizationIntegrationConfiguration
        {
            Configuration = null,
            Template = "template"
        };
        Assert.True(_sut.ValidateConfiguration(IntegrationType.Datadog, config2));

        var config3 = new OrganizationIntegrationConfiguration
        {
            Configuration = null,
            Template = "template"
        };
        Assert.True(_sut.ValidateConfiguration(IntegrationType.Teams, config3));
    }

    [Fact]
    public void ValidateConfiguration_InvalidJsonConfiguration_ReturnsFalse()
    {
        var config = new OrganizationIntegrationConfiguration
        {
            Configuration = "{not valid json}",
            Template = "template"
        };

        Assert.False(_sut.ValidateConfiguration(IntegrationType.Slack, config));
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Webhook, config));
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Hec, config));
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Datadog, config));
        Assert.False(_sut.ValidateConfiguration(IntegrationType.Teams, config));
    }

    [Fact]
    public void ValidateConfiguration_InvalidJsonFilters_ReturnsFalse()
    {
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = JsonSerializer.Serialize(new WebhookIntegrationConfiguration(Uri: new Uri("https://example.com"))),
            Template = "template",
            Filters = "{Not valid json}"
        };

        Assert.False(_sut.ValidateConfiguration(IntegrationType.Webhook, configuration));
    }

    [Fact]
    public void ValidateConfiguration_ScimIntegration_ReturnsFalse()
    {
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = "{}",
            Template = "template"
        };

        Assert.False(_sut.ValidateConfiguration(IntegrationType.Scim, configuration));
    }

    [Fact]
    public void ValidateConfiguration_ValidSlackConfiguration_ReturnsTrue()
    {
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = JsonSerializer.Serialize(new SlackIntegrationConfiguration(ChannelId: "C12345")),
            Template = "template"
        };

        Assert.True(_sut.ValidateConfiguration(IntegrationType.Slack, configuration));
    }

    [Fact]
    public void ValidateConfiguration_ValidSlackConfigurationWithFilters_ReturnsTrue()
    {
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = JsonSerializer.Serialize(new SlackIntegrationConfiguration("C12345")),
            Template = "template",
            Filters = JsonSerializer.Serialize(new IntegrationFilterGroup()
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
            })
        };

        Assert.True(_sut.ValidateConfiguration(IntegrationType.Slack, configuration));
    }

    [Fact]
    public void ValidateConfiguration_ValidNoAuthWebhookConfiguration_ReturnsTrue()
    {
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = JsonSerializer.Serialize(new WebhookIntegrationConfiguration(Uri: new Uri("https://localhost"))),
            Template = "template"
        };

        Assert.True(_sut.ValidateConfiguration(IntegrationType.Webhook, configuration));
    }

    [Fact]
    public void ValidateConfiguration_ValidWebhookConfiguration_ReturnsTrue()
    {
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = JsonSerializer.Serialize(new WebhookIntegrationConfiguration(
                Uri: new Uri("https://localhost"),
                Scheme: "Bearer",
                Token: "AUTH-TOKEN")),
            Template = "template"
        };

        Assert.True(_sut.ValidateConfiguration(IntegrationType.Webhook, configuration));
    }

    [Fact]
    public void ValidateConfiguration_ValidWebhookConfigurationWithFilters_ReturnsTrue()
    {
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = JsonSerializer.Serialize(new WebhookIntegrationConfiguration(
                Uri: new Uri("https://example.com"),
                Scheme: "Bearer",
                Token: "AUTH-TOKEN")),
            Template = "template",
            Filters = JsonSerializer.Serialize(new IntegrationFilterGroup()
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
            })
        };

        Assert.True(_sut.ValidateConfiguration(IntegrationType.Webhook, configuration));
    }

    [Fact]
    public void ValidateConfiguration_UnknownIntegrationType_ReturnsFalse()
    {
        var unknownType = (IntegrationType)999;
        var configuration = new OrganizationIntegrationConfiguration
        {
            Configuration = "{}",
            Template = "template"
        };

        Assert.False(_sut.ValidateConfiguration(unknownType, configuration));
    }
}
