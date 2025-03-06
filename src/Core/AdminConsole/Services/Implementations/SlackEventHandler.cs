using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class SlackEventHandler(
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    SlackMessageSender slackMessageSender
    ) : IEventMessageHandler
{
    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        Guid organizationId = eventMessage.OrganizationId ?? Guid.NewGuid();

        var configuration = await configurationRepository.GetConfigurationAsync<SlackConfiguration>(
            organizationId,
            IntegrationType.Slack,
            eventMessage.Type);
        if (configuration is not null)
        {
            await slackMessageSender.SendDirectMessageByEmailAsync(
                configuration.Configuration.Token,
                configuration.Template,
                configuration.Configuration.UserEmails.First()
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
