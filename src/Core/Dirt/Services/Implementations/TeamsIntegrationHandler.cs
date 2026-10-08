using System.Text.Json;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Microsoft.Bot.Schema;

namespace Bit.Core.Dirt.Services.Implementations;

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

            return IntegrationHandlerResult.Succeed(message);
        }
        catch (ErrorResponseException ex) when (ex.Response is not null)
        {
            var category = ClassifyHttpStatusCode(ex.Response.StatusCode);
            return IntegrationHandlerResult.Fail(
                message,
                category,
                ex.Message
            );
        }
        catch (ArgumentException ex)
        {
            return IntegrationHandlerResult.Fail(
                message,
                IntegrationFailureCategory.ConfigurationError,
                ex.Message
            );
        }
        catch (UriFormatException ex)
        {
            return IntegrationHandlerResult.Fail(
                message,
                IntegrationFailureCategory.ConfigurationError,
                ex.Message
            );
        }
        catch (JsonException ex)
        {
            return IntegrationHandlerResult.Fail(
                message,
                IntegrationFailureCategory.PermanentFailure,
                ex.Message
            );
        }
        catch (Exception ex)
        {
            return IntegrationHandlerResult.Fail(
                message,
                IntegrationFailureCategory.TransientError,
                ex.Message
            );
        }
    }
}
