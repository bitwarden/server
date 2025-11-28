#nullable enable

using System.Text.Json;
using Bit.Core.Models.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public abstract class EventLoggingListenerService : BackgroundService
{
    protected readonly IEventMessageHandler _handler;
    protected ILogger _logger;

    protected EventLoggingListenerService(IEventMessageHandler handler, ILogger logger)
    {
        _handler = handler;
        _logger = logger;
    }

    internal async Task ProcessReceivedMessageAsync(string body, string? messageId)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(body);
            var root = jsonDocument.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var eventMessages = root.Deserialize<IEnumerable<EventMessage>>();
                await _handler.HandleManyEventsAsync(eventMessages ?? throw new JsonException("Deserialize returned null"));
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                var eventMessage = root.Deserialize<EventMessage>();
                await _handler.HandleEventAsync(eventMessage ?? throw new JsonException("Deserialize returned null"));
            }
            else
            {
                if (!string.IsNullOrEmpty(messageId))
                {
                    _logger.LogError("An error occurred while processing message: {MessageId} - Invalid JSON", messageId);
                }
                else
                {
                    _logger.LogError("An Invalid JSON error occurred while processing a message with an empty message id");
                }
            }
        }
        catch (JsonException exception)
        {
            if (!string.IsNullOrEmpty(messageId))
            {
                _logger.LogError(
                    exception,
                    "An error occurred while processing message: {MessageId} - Invalid JSON",
                    messageId
                );
            }
            else
            {
                _logger.LogError(
                    exception,
                    "An Invalid JSON error occurred while processing a message with an empty message id"
                );
            }
        }
        catch (Exception exception)
        {
            if (!string.IsNullOrEmpty(messageId))
            {
                _logger.LogError(
                    exception,
                    "An error occurred while processing message: {MessageId}",
                    messageId
                );
            }
            else
            {
                _logger.LogError(
                    exception,
                    "An error occurred while processing a message with an empty message id"
                );
            }
        }
    }
}
