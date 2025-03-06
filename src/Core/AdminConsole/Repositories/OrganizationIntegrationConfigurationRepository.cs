using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Settings;

namespace Bit.Core.Repositories;

#nullable enable

public class OrganizationIntegrationConfigurationRepository(GlobalSettings globalSettings)
    : IOrganizationIntegrationConfigurationRepository
{
    private readonly string _slackToken = globalSettings.EventLogging.SlackToken;
    private readonly string _slackUserEmail = globalSettings.EventLogging.SlackUserEmail;
    private readonly string _webhookUrl = globalSettings.EventLogging.WebhookUrl;

    public async Task<IntegrationConfiguration<T>?> GetConfigurationAsync<T>(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType)
    {
        switch (integrationType)
        {
            case IntegrationType.Slack:
                if (string.IsNullOrWhiteSpace(_slackToken) || string.IsNullOrWhiteSpace(_slackUserEmail))
                {
                    return null;
                }
                return new IntegrationConfiguration<SlackConfiguration>()
                {
                    Configuration = new SlackConfiguration
                    {
                        Token = _slackToken,
                        Channels = new List<string> { },
                        UserEmails = new List<string> { _slackUserEmail }
                    },
                    Template = "This is a test of the new Slack integration"
                } as IntegrationConfiguration<T>;
            case IntegrationType.Webhook:
                if (string.IsNullOrWhiteSpace(_webhookUrl))
                {
                    return null;
                }
                return new IntegrationConfiguration<WebhookConfiguration>()
                {
                    Configuration = new WebhookConfiguration()
                    {
                        Url = _webhookUrl,
                    },
                    Template = "{ \"newObject\": true }"
                } as IntegrationConfiguration<T>;
            default:
                return null;
        }
    }

    public async Task<IEnumerable<IntegrationConfiguration<T>>> GetAllConfigurationsAsync<T>(Guid organizationId) => throw new NotImplementedException();

    public async Task AddConfigurationAsync<T>(Guid organizationId, IntegrationType integrationType, EventType eventType,
        IntegrationConfiguration<T> configuration) =>
        throw new NotImplementedException();

    public async Task UpdateConfigurationAsync<T>(IntegrationConfiguration<T> configuration) => throw new NotImplementedException();

    public async Task DeleteConfigurationAsync(Guid id) => throw new NotImplementedException();
}
