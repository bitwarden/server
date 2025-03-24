﻿using System.Text;
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
        Guid organizationId = eventMessage.OrganizationId ?? Guid.NewGuid();

        var configurations = await configurationRepository.GetConfigurationsAsync<WebhookConfiguration>(organizationId,
            IntegrationType.Webhook, eventMessage.Type);

        foreach (var configuration in configurations)
        {
            var content = new StringContent(
                TemplateProcessor.ReplaceTokens(configuration.Template, eventMessage),
                Encoding.UTF8,
                "application/json"
            );
            var response = await _httpClient.PostAsync(
                configuration.Configuration.Url,
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
