using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Settings;

namespace Bit.Core.Repositories;

public class LocalOrganizationIntegrationConfigurationRepository(GlobalSettings globalSettings)
    : IOrganizationIntegrationConfigurationRepository
{
    public async Task<List<IntegrationConfiguration<T>>> GetConfigurationsAsync<T>(Guid organizationId,
        IntegrationType integrationType,
        EventType eventType)
    {
        var configurations = new List<IntegrationConfiguration<T>>();
        switch (integrationType)
        {
            case IntegrationType.Slack:
                foreach (var configuration in globalSettings.EventLogging.SlackConfigurations)
                {
                    configurations.Add(new IntegrationConfiguration<SlackConfiguration>
                    {
                        Configuration = configuration,
                        Template = "This is a test of the new Slack integration, #UserId#, #Type#, #Date#"
                    } as IntegrationConfiguration<T>);
                }
                break;
            case IntegrationType.Webhook:
                foreach (var configuration in globalSettings.EventLogging.WebhookConfigurations)
                {
                    configurations.Add(new IntegrationConfiguration<WebhookConfiguration>
                    {
                        Configuration = configuration,
                        Template = "{ \"Date\": \"#Date#\", \"Type\": \"#Type#\", \"UserId\": \"#UserId#\" }"
                    } as IntegrationConfiguration<T>);
                }
                break;
        }

        return configurations;
    }

    public async Task CreateOrganizationIntegrationAsync<T>(
        Guid organizationId,
        IntegrationType integrationType,
        T configuration)
    {
        var json = JsonSerializer.Serialize(configuration);

        Console.WriteLine($"Organization: {organizationId}, IntegrationType: {integrationType}, Configuration: {json}");
    }
}
