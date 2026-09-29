using System.Text.Json;
using System.Text.Json.Nodes;
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
            case IntegrationType.Teams:
                // Null sends to the channel the app was installed in (the team's General channel).
                return configuration.Configuration is null || IsTeamsConfigurationValid(configuration.Configuration);
            case IntegrationType.Hec:
            case IntegrationType.Datadog:
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

    /// <summary>
    /// The key must be exactly <c>ChannelId</c> so it replaces the integration's install channel when the two
    /// configurations are merged (see <see cref="OrganizationIntegrationConfigurationDetails.MergedConfiguration"/>).
    /// A differently cased key would be added alongside it instead. No other keys are allowed, because any key here
    /// overrides the install-time values (such as <c>ServiceUrl</c>) that only the Bot Framework callback may set.
    /// </summary>
    private static bool IsTeamsConfigurationValid(string configuration)
    {
        try
        {
            return JsonNode.Parse(configuration) is JsonObject json
                && json.Count == 1
                && json[nameof(TeamsIntegrationConfiguration.ChannelId)] is JsonValue channelId
                && channelId.TryGetValue<string>(out var value)
                && !string.IsNullOrWhiteSpace(value);
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
