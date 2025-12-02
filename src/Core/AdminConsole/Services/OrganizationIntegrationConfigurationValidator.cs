using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Services;

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

        switch (integrationType)
        {
            case IntegrationType.CloudBillingSync or IntegrationType.Scim:
                return false;
            case IntegrationType.Slack:
                return IsConfigurationValid<SlackIntegrationConfiguration>(configuration.Configuration) &&
                       IsFiltersValid(configuration.Filters);
            case IntegrationType.Webhook:
                return IsConfigurationValid<WebhookIntegrationConfiguration>(configuration.Configuration) &&
                       IsFiltersValid(configuration.Filters);
            case IntegrationType.Hec:
            case IntegrationType.Datadog:
            case IntegrationType.Teams:
                return configuration.Configuration is null &&
                       IsFiltersValid(configuration.Filters);
            default:
                return false;
        }
    }

    private bool IsConfigurationValid<T>(string? configuration)
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

    private bool IsFiltersValid(string? filters)
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
