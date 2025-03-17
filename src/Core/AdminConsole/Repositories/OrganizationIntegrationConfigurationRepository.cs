using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Settings;

namespace Bit.Core.Repositories;

public class OrganizationIntegrationConfigurationRepository(GlobalSettings globalSettings)
    : IOrganizationIntegrationConfigurationRepository
{
    public async Task<List<IntegrationConfiguration<T>>> GetConfigurationsAsync<T>(IntegrationType integrationType,
        Guid organizationId,
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

    public async Task<IEnumerable<IntegrationConfiguration<T>>> GetAllConfigurationsAsync<T>(Guid organizationId) => throw new NotImplementedException();

    public async Task AddConfigurationAsync<T>(Guid organizationId, IntegrationType integrationType, EventType eventType,
        IntegrationConfiguration<T> configuration) =>
        throw new NotImplementedException();

    public async Task UpdateConfigurationAsync<T>(IntegrationConfiguration<T> configuration) => throw new NotImplementedException();

    public async Task DeleteConfigurationAsync(Guid id) => throw new NotImplementedException();
}
