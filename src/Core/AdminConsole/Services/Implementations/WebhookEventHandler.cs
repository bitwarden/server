using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Services;

public class WebhookEventHandler(
    IHttpClientFactory httpClientFactory,
    IOrganizationIntegrationConfigurationRepository configurationRepository)
    : IEventMessageHandler
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "WebhookEventHandlerHttpClient";

    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        var organizationId = eventMessage.OrganizationId ?? Guid.Empty;
        var configurations = await configurationRepository.GetConfigurationDetailsAsync(
            organizationId,
            IntegrationType.Webhook,
            eventMessage.Type);

        foreach (var configuration in configurations)
        {
            var config = configuration.MergedConfiguration.Deserialize<WebhookIntegrationConfigurationDetils>();
            if (config is null || string.IsNullOrEmpty(config.url))
            {
                continue;
            }

            var content = new StringContent(
                IntegrationTemplateProcessor.ReplaceTokens(configuration.Template, eventMessage),
                Encoding.UTF8,
                "application/json"
            );
            var response = await _httpClient.PostAsync(
                config.url,
                content);
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task HandleManyEventsAsync(IEnumerable<EventMessage> eventMessages)
    {
        foreach (var eventMessage in eventMessages)
        {
            await HandleEventAsync(eventMessage);
        }
    }
}
