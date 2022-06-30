using System.Text.Json;
using Azure.Storage.Queues;
using Bit.Core;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.EventsProcessor;

public class AzureQueueHostedService : IHostedService, IDisposable
{
    private readonly ILogger<AzureQueueHostedService> _logger;
    private readonly IConfiguration _configuration;

    private Task _executingTask;
    private CancellationTokenSource _cts;
    private QueueClient _queueClient;
    private IEventWriteService _eventWriteService;

    public AzureQueueHostedService(
        ILogger<AzureQueueHostedService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Starting service.");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = ExecuteAsync(_cts.Token);
        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
        {
            return;
        }
        _logger.LogWarning("Stopping service.");
        _cts.Cancel();
        await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose()
    { }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var storageConnectionString = _configuration["azureStorageConnectionString"];
        if (string.IsNullOrWhiteSpace(storageConnectionString))
        {
            return;
        }

        var repo = new Core.Repositories.TableStorage.EventRepository(storageConnectionString);
        _eventWriteService = new RepositoryEventWriteService(repo);
        _queueClient = new QueueClient(storageConnectionString, "event");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _queueClient.ReceiveMessagesAsync(32);
                if (messages.Value?.Any() ?? false)
                {
                    foreach (var message in messages.Value)
                    {
                        await ProcessQueueMessageAsync(message.DecodeMessageText(), cancellationToken);
                        await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception occurred: " + e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        _logger.LogWarning("Done processing.");
    }

    public async Task ProcessQueueMessageAsync(string message, CancellationToken cancellationToken)
    {
        if (_eventWriteService == null || message == null || message.Length == 0)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Processing message.");
            var events = new List<IEvent>();

            using var jsonDocument = JsonDocument.Parse(message);
            var root = jsonDocument.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var indexedEntities = root.ToObject<List<EventMessage>>()
                    .SelectMany(e => EventTableEntity.IndexEvent(e));
                events.AddRange(indexedEntities);
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                var eventMessage = root.ToObject<EventMessage>();
                events.AddRange(EventTableEntity.IndexEvent(eventMessage));
            }

            await _eventWriteService.CreateManyAsync(events);
            _logger.LogInformation("Processed message.");
        }
        catch (JsonException)
        {
            _logger.LogError("JsonReaderException: Unable to parse message.");
        }
    }
}
