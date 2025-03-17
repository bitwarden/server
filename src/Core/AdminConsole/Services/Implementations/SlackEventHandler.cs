using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class SlackEventHandler(
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    ISlackService slackService
    ) : IEventMessageHandler
{
    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        var organizationId = eventMessage.OrganizationId ?? Guid.NewGuid();
        var configurations = await configurationRepository.GetConfigurationsAsync<SlackConfiguration>(
            IntegrationType.Slack,
            organizationId, eventMessage.Type
        );

        foreach (var configuration in configurations)
        {
            await slackService.SendSlackMessageByChannelId(
                configuration.Configuration.Token,
                TemplateProcessor.ReplaceTokens(configuration.Template, eventMessage),
                configuration.Configuration.ChannelId
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
