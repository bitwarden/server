using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class SlackEventHandler(
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    ISlackService slackService)
    : IEventMessageHandler
{
    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        var organizationId = eventMessage.OrganizationId ?? Guid.Empty;
        var configurations = await configurationRepository.GetConfigurationDetailsAsync(
            organizationId,
            IntegrationType.Slack,
            eventMessage.Type);

        foreach (var configuration in configurations)
        {
            var config = configuration.MergedConfiguration.Deserialize<SlackIntegrationConfigurationDetails>();
            if (config is null)
            {
                continue;
            }

            await slackService.SendSlackMessageByChannelIdAsync(
                config.token,
                TemplateProcessor.ReplaceTokens(configuration.Template, eventMessage),
                config.channelId
            );
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
