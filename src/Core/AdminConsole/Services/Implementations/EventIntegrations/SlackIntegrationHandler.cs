#nullable enable

using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public class SlackIntegrationHandler(
    ISlackService slackService)
    : IntegrationHandlerBase<SlackIntegrationConfigurationDetails>
{
    public override async Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<SlackIntegrationConfigurationDetails> message)
    {
        await slackService.SendSlackMessageByChannelIdAsync(
            message.Configuration.Token,
            message.RenderedTemplate,
            message.Configuration.ChannelId
        );

        return new IntegrationHandlerResult(success: true, message: message);
    }
}
