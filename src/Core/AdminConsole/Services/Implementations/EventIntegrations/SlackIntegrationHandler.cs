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
            return IntegrationHandlerResult.Fail(
                message,
                IntegrationFailureCategory.TransientError,
                "Slack response was null"
            );
        }

        if (slackResponse.Ok)
        {
            return IntegrationHandlerResult.Succeed(message);
        }

        var category = ClassifySlackError(slackResponse.Error);
        return IntegrationHandlerResult.Fail(
            message,
            category,
            slackResponse.Error
        );
    }

    /// <summary>
    /// Classifies a Slack API error code string as an <see cref="IntegrationFailureCategory"/> to drive
    /// retry behavior and operator-facing failure reporting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Slack responses commonly return an <c>error</c> string when <c>ok</c> is false. This method maps
    /// known Slack error codes to failure categories.
    /// </para>
    /// <para>
    /// Any unrecognized error codes default to <see cref="IntegrationFailureCategory.TransientError"/> to avoid
    /// incorrectly marking new/unknown Slack failures as non-retryable.
    /// </para>
    /// </remarks>
    /// <param name="error">The Slack error code string (e.g. <c>invalid_auth</c>, <c>rate_limited</c>).</param>
    /// <returns>The corresponding <see cref="IntegrationFailureCategory"/>.</returns>
    private static IntegrationFailureCategory ClassifySlackError(string error)
    {
        return error switch
        {
            "invalid_auth" => IntegrationFailureCategory.AuthenticationFailed,
            "access_denied" => IntegrationFailureCategory.AuthenticationFailed,
            "token_expired" => IntegrationFailureCategory.AuthenticationFailed,
            "token_revoked" => IntegrationFailureCategory.AuthenticationFailed,
            "account_inactive" => IntegrationFailureCategory.AuthenticationFailed,
            "not_authed" => IntegrationFailureCategory.AuthenticationFailed,
            "channel_not_found" => IntegrationFailureCategory.ConfigurationError,
            "is_archived" => IntegrationFailureCategory.ConfigurationError,
            "rate_limited" => IntegrationFailureCategory.RateLimited,
            "ratelimited" => IntegrationFailureCategory.RateLimited,
            "message_limit_exceeded" => IntegrationFailureCategory.RateLimited,
            "internal_error" => IntegrationFailureCategory.TransientError,
            "service_unavailable" => IntegrationFailureCategory.ServiceUnavailable,
            "fatal_error" => IntegrationFailureCategory.ServiceUnavailable,
            _ => IntegrationFailureCategory.TransientError
        };
    }
}
