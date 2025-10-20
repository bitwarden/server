using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public class TeamsIntegrationHandler(
    ITeamsService teamsService)
    : IntegrationHandlerBase<TeamsIntegrationConfigurationDetails>
{
    public override async Task<IntegrationHandlerResult> HandleAsync(
        IntegrationMessage<TeamsIntegrationConfigurationDetails> message)
    {
        try
        {
            await teamsService.SendMessageToChannelAsync(
                serviceUri: message.Configuration.ServiceUrl,
                message: message.RenderedTemplate,
                channelId: message.Configuration.ChannelId
            );

            return new IntegrationHandlerResult(success: true, message: message);
        }
        catch (HttpOperationException ex)
        {
            var result = new IntegrationHandlerResult(success: false, message: message);
            var statusCode = (int)ex.Response.StatusCode;
            result.Retryable = statusCode is 429 or >= 500 and < 600;
            result.FailureReason = ex.Message;

            return result;
        }
        catch (Exception ex)
        {
            var result = new IntegrationHandlerResult(success: false, message: message);
            result.Retryable = false;
            result.FailureReason = ex.Message;

            return result;
        }
    }
}
