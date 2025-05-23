using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.AdminConsole.Models.Data.Integrations;
using Bit.Core.Enums;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Services;

public class SlackEventHandler(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    ISlackService slackService)
    : IntegrationEventHandlerBase(userRepository, organizationRepository, configurationRepository)
{
    protected override IntegrationType GetIntegrationType() => IntegrationType.Slack;

    protected override async Task ProcessEventIntegrationAsync(JsonObject mergedConfiguration,
        string renderedTemplate)
    {
        var config = mergedConfiguration.Deserialize<SlackIntegrationConfigurationDetails>();
        if (config is null)
        {
            return;
        }

        await slackService.SendSlackMessageByChannelIdAsync(
            config.token,
            renderedTemplate,
            config.channelId
        );
    }
}
