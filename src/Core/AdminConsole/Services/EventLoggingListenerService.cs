#nullable enable

using System.Text.Json;
using Bit.Core.Models.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public abstract class EventLoggingListenerService : BackgroundService
{
    protected readonly IEventMessageHandler _handler;
    protected ILogger<EventLoggingListenerService> _logger;

    protected EventLoggingListenerService(IEventMessageHandler handler, ILogger<EventLoggingListenerService> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    internal async Task ProcessReceivedMessageAsync(string body, string messageId)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(body);
            var root = jsonDocument.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var eventMessages = root.Deserialize<IEnumerable<EventMessage>>();
                await _handler.HandleManyEventsAsync(eventMessages);
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                var eventMessage = root.Deserialize<EventMessage>();
                await _handler.HandleEventAsync(eventMessage);
            }
            else
            {
                _logger.LogError($"An error occurred while processing message {messageId} - Invalid JSON");
            }
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "An error occured while processing message: {MessageId} - Invalid JSON",
                messageId
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "An error occured while processing message: {MessageId}",
                messageId
            );
        }
    }
}
