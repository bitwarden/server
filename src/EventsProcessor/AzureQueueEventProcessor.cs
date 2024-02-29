using System.Text.Json;
using Azure.Storage.Queues;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Core.Repositories.TableStorage;
using System.Buffers.Text;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Bit.EventsProcessor;

#nullable enable

public interface IProcessor
{
    /// <summary>
    /// Process items
    /// </summary>
    /// <returns>Returns whether or not something was processed.</returns>
    Task<bool> ProcessAsync(CancellationToken cancellationToken);
}

public class AzureQueueEventProcessor : IProcessor
{
    private readonly ILogger<AzureQueueEventProcessor> _logger;
    private readonly IConfiguration _configuration;

    private readonly RepositoryEventWriteService _eventWriteService;
    private readonly QueueClient _queueClient;

    public AzureQueueEventProcessor(ILogger<AzureQueueEventProcessor> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        var storageConnectionString = _configuration["azureStorageConnectionString"];

        if (!string.IsNullOrWhiteSpace(storageConnectionString))
        {
            var eventRepository = new AzureTablesEventRepository(storageConnectionString);
            _eventWriteService = new RepositoryEventWriteService(eventRepository);
            _queueClient = new QueueClient(storageConnectionString, "event");
        }
        else
        {
            _eventWriteService = null!;
            _queueClient = null!;
        }
    }

    public async Task<bool> ProcessAsync(CancellationToken cancellationToken)
    {
        var messages = await _queueClient.ReceiveMessagesAsync(32, cancellationToken: cancellationToken);
        if (!messages.HasValue)
        {
            return false;
        }

        foreach (var message in messages.Value)
        {
            await ProcessQueueMessageAsync(message.Body.ToMemory());
            await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, CancellationToken.None);
        }

        return true;
    }

    private async Task ProcessQueueMessageAsync(ReadOnlyMemory<byte> message)
    {
        if (!TryGetMessages(message, out var messages))
        {
            return;
        }

        await _eventWriteService.CreateManyAsync(messages);
        _logger.LogInformation("Processed message.");
    }

    private bool TryGetMessages(ReadOnlyMemory<byte> message, [NotNullWhen(true)] out IReadOnlyList<IEvent>? messages)
    {
        const int StackAllocThreshold = 256;
        byte[]? rentedBuffer = null;
        var decodedLength = Base64.GetMaxEncodedToUtf8Length(message.Length);

        Span<byte> buffer = decodedLength > StackAllocThreshold
            ? (rentedBuffer = ArrayPool<byte>.Shared.Rent(decodedLength))
            : stackalloc byte[StackAllocThreshold];

        try
        {
            // TODO: Do we want to assert on the outs at all?
            // TODO: Check operation status
            var status = Base64.EncodeToUtf8(message.Span, buffer, out _, out _);
            Debug.Assert(status == OperationStatus.Done, $"Operation instead has status {status}");

            using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer)!;
            var root = jsonDocument.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                messages = root.Deserialize<List<EventMessage>>()!
                    .SelectMany(e => EventTableEntity.IndexEvent(e))
                    .ToList();
                return true;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                var eventMessage = root.Deserialize<EventMessage>();
                messages = EventTableEntity.IndexEvent(eventMessage);
                return true;
            }
        }
        catch (JsonException jsonException)
        {
            _logger.LogError(jsonException, "Unable to parse message");
            throw jsonException;
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer, true);
            }
        }

        messages = null;
        return false;
    }
}
