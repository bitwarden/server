using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;

#nullable enable

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationIntegrationConfigurationRequestModel
{
    public string? Configuration { get; set; }

    [Required]
    public EventType EventType { get; set; }

    public string? Template { get; set; }

    public bool IsValidForType(IntegrationType integrationType)
    {
        switch (integrationType)
        {
            case IntegrationType.CloudBillingSync or IntegrationType.Scim:
                return false;
            case IntegrationType.Slack:
                return !string.IsNullOrWhiteSpace(Template) && IsConfigurationValid<SlackIntegrationConfiguration>();
            case IntegrationType.Webhook:
                return !string.IsNullOrWhiteSpace(Template) && IsConfigurationValid<WebhookIntegrationConfiguration>();
            default:
                return false;

        }
    }

    public OrganizationIntegrationConfiguration ToOrganizationIntegrationConfiguration(Guid organizationIntegrationId)
    {
        return new OrganizationIntegrationConfiguration()
        {
            OrganizationIntegrationId = organizationIntegrationId,
            Configuration = Configuration,
            EventType = EventType,
            Template = Template
        };
    }

    public OrganizationIntegrationConfiguration ToOrganizationIntegrationConfiguration(OrganizationIntegrationConfiguration currentConfiguration)
    {
        currentConfiguration.Configuration = Configuration;
        currentConfiguration.EventType = EventType;
        currentConfiguration.Template = Template;

        return currentConfiguration;
    }

    private bool IsConfigurationValid<T>()
    {
        if (string.IsNullOrWhiteSpace(Configuration))
        {
            return false;
        }

        try
        {
            var config = JsonSerializer.Deserialize<T>(Configuration);
            return config is not null;
        }
        catch
        {
            return false;
        }
    }
}
