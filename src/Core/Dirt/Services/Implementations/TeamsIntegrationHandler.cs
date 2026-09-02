using System.Text.Json;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Microsoft.Rest;

namespace Bit.Core.Dirt.Services.Implementations;

public class TeamsIntegrationHandler(
    ITeamsService teamsService)
    : IntegrationHandlerBase<TeamsIntegrationConfigurationDetails>
{
    public override async Task<IntegrationHandlerResult> HandleAsync(
        IntegrationMessage<TeamsIntegrationConfigurationDetails> message)
    {
        var channelId = message.Configuration.ChannelId;
        var serviceUrl = message.Configuration.ServiceUrl;

        // The integration is either awaiting its app install callback or has been disconnected; there is no
        // channel to deliver to and no amount of retrying will produce one.
        if (string.IsNullOrEmpty(channelId) || serviceUrl is null)
        {
            return IntegrationHandlerResult.Fail(
                message,
                IntegrationFailureCategory.ConfigurationError,
                "Teams integration is not connected to a channel."
            );
        }

        try
        {
            await teamsService.SendMessageToChannelAsync(
                serviceUri: serviceUrl,
                message: message.RenderedTemplate,
                channelId: channelId
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
