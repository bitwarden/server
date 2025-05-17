using Bit.Core.Models.Data.Integrations;

namespace Bit.Core.Services;

public class SlackIntegrationHandler(
    ISlackService slackService)
    : IntegrationHandlerBase<SlackIntegrationConfigurationDetails>
{
    public override async Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<SlackIntegrationConfigurationDetails> message)
    {
        await slackService.SendSlackMessageByChannelIdAsync(
            message.Configuration.token,
            message.RenderedTemplate,
            message.Configuration.channelId
        );

        return new IntegrationHandlerResult(success: true, message: message);
    }
}
