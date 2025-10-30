using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public class SlackIntegrationHandler(
    ISlackService slackService)
    : IntegrationHandlerBase<SlackIntegrationConfigurationDetails>
{
    public override async Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<SlackIntegrationConfigurationDetails> message)
    {
        var slackResponse = await slackService.SendSlackMessageByChannelIdAsync(
            message.Configuration.Token,
            message.RenderedTemplate,
            message.Configuration.ChannelId
        );

        if (slackResponse is null)
        {
            return new IntegrationHandlerResult(success: false, message: message)
            {
                FailureReason = "Slack response was null"
            };
        }

        if (slackResponse.Ok)
        {
            return new IntegrationHandlerResult(success: true, message: message);
        }

        var result = new IntegrationHandlerResult(success: false, message: message);
        result.FailureReason = slackResponse.Error;
        if (slackResponse.Error.Equals("internal_error") ||
            slackResponse.Error.Equals("message_limit_exceeded") ||
            slackResponse.Error.Equals("rate_limited") ||
            slackResponse.Error.Equals("ratelimited") ||
            slackResponse.Error.Equals("service_unavailable")
        )
        {
            result.Retryable = true;
        }

        return result;
    }
}
