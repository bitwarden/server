using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class AzureServiceBusService : IAzureServiceBusService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _eventSender;
    private readonly ServiceBusSender _integrationSender;

    private readonly int _batchSize;
    private readonly ConcurrentQueue<string> _eventBuffer = new();
    private int _eventBufferCount = 0;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly Timer _flushTimer;

    public AzureServiceBusService(GlobalSettings globalSettings)
    {
        _client = new ServiceBusClient(globalSettings.EventLogging.AzureServiceBus.ConnectionString);
        _eventSender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.EventTopicName);
        _integrationSender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName);
        _batchSize = globalSettings.EventLogging.AzureServiceBus.SenderBatchSize;
        var flushInterval = TimeSpan.FromMilliseconds(globalSettings.EventLogging.AzureServiceBus.SenderFlushInterval);
        _flushTimer = new Timer(_ => _ = FlushAsync(), null, flushInterval, flushInterval);
    }

    public ServiceBusProcessor CreateProcessor(string topicName, string subscriptionName, ServiceBusProcessorOptions options)
    {
        return _client.CreateProcessor(topicName, subscriptionName, options);
    }

    public async Task PublishAsync(IIntegrationMessage message)
    {
        var json = message.ToJson();

        var serviceBusMessage = new ServiceBusMessage(json)
        {
            Subject = message.IntegrationType.ToRoutingKey(),
            MessageId = message.MessageId
        };

        await _integrationSender.SendMessageAsync(serviceBusMessage);
    }

    public async Task PublishToRetryAsync(IIntegrationMessage message)
    {
        var json = message.ToJson();

        var serviceBusMessage = new ServiceBusMessage(json)
        {
            Subject = message.IntegrationType.ToRoutingKey(),
            ScheduledEnqueueTime = message.DelayUntilDate ?? DateTime.UtcNow,
            MessageId = message.MessageId
        };

        await _integrationSender.SendMessageAsync(serviceBusMessage);
    }

    public Task PublishEventAsync(string body)
    {
        _eventBuffer.Enqueue(body);
        if (Interlocked.Increment(ref _eventBufferCount) >= _batchSize)
        {
            _ = FlushAsync();
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _flushTimer.DisposeAsync();
        await FlushAsync();
        await _eventSender.DisposeAsync();
        await _integrationSender.DisposeAsync();
        await _client.DisposeAsync();
    }

    private async Task FlushAsync()
    {
        if (_eventBuffer.IsEmpty) return;
        if (!await _flushLock.WaitAsync(0)) return;

        try
        {
            var added = 0;
            using var batch = await _eventSender.CreateMessageBatchAsync();

            while (added < _batchSize && _eventBuffer.TryDequeue(out var body))
            {
                var message = new ServiceBusMessage(body)
                {
                    ContentType = "application/json",
                    MessageId = Guid.NewGuid().ToString()
                };

                if (!batch.TryAddMessage(message))
                {
                    _eventBuffer.Enqueue(body);
                    break; // Batch full, send what we have
                }

                added++;
                Interlocked.Decrement(ref _eventBufferCount);
            }

            if (batch.Count > 0)
            {
                await _eventSender.SendMessagesAsync(batch);
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }
}
