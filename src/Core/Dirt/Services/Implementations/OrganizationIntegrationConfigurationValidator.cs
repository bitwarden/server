using System.Text.Json;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;

namespace Bit.Core.Dirt.Services.Implementations;

public class OrganizationIntegrationConfigurationValidator : IOrganizationIntegrationConfigurationValidator
{
    public bool ValidateConfiguration(IntegrationType integrationType,
        OrganizationIntegrationConfiguration configuration)
    {
        // Validate template is present
        if (string.IsNullOrWhiteSpace(configuration.Template))
        {
            return false;
        }
        // If Filters are present, they must be valid
        if (!IsFiltersValid(configuration.Filters))
        {
            return false;
        }

        switch (integrationType)
        {
            case IntegrationType.CloudBillingSync or IntegrationType.Scim:
                return false;
            case IntegrationType.Slack:
                return IsConfigurationValid<SlackIntegrationConfiguration>(configuration.Configuration);
            case IntegrationType.Webhook:
                return IsConfigurationValid<WebhookIntegrationConfiguration>(configuration.Configuration);
            case IntegrationType.Hec:
            case IntegrationType.Datadog:
            case IntegrationType.Teams:
                return configuration.Configuration is null;
            default:
                return false;
        }
    }

    private static bool IsConfigurationValid<T>(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return false;
        }

        try
        {
            var config = JsonSerializer.Deserialize<T>(configuration);
            return config is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFiltersValid(string? filters)
    {
        if (filters is null)
        {
            return true;
        }

        try
        {
            var filterGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(filters);
            return filterGroup is not null;
        }
        catch
        {
            return false;
        }
    }
}
