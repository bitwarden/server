using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Services;

public class WebhookEventHandler(
    IHttpClientFactory httpClientFactory,
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository)
    : IntegrationEventHandlerBase(userRepository, organizationRepository, configurationRepository)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "WebhookEventHandlerHttpClient";

    protected override IntegrationType GetIntegrationType() => IntegrationType.Webhook;

    protected override async Task ProcessEventIntegrationAsync(JsonObject mergedConfiguration,
        string renderedTemplate)
    {
        var config = mergedConfiguration.Deserialize<WebhookIntegrationConfigurationDetils>();
        if (config is null || string.IsNullOrEmpty(config.url))
        {
            return;
        }

        var content = new StringContent(renderedTemplate, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(config.url, content);
        response.EnsureSuccessStatusCode();
    }
}
