using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public class SlackIntegrationHandler(
    ISlackService slackService)
    : IntegrationHandlerBase<SlackIntegrationConfigurationDetails>
{
    private static readonly HashSet<string> _retryableErrors = new(StringComparer.Ordinal)
        {
            "internal_error",
            "message_limit_exceeded",
            "rate_limited",
            "ratelimited",
            "service_unavailable"
        };

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

        var result = new IntegrationHandlerResult(success: false, message: message) { FailureReason = slackResponse.Error };

        if (_retryableErrors.Contains(slackResponse.Error))
        {
            result.Retryable = true;
        }

        return result;
    }
}
