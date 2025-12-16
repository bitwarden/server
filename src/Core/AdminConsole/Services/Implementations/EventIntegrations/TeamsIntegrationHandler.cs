using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Microsoft.Rest;

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

            return IntegrationHandlerResult.Succeed(message);
        }
        catch (HttpOperationException ex)
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
