using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureServiceBusEventBatchBackgroundService(ServiceBusSender sender, int batchSize, ILogger logger) : BackgroundService
{
    private readonly ConcurrentQueue<string> _queue = new();

    public void Enqueue(string body)
    {
        _queue.Enqueue(body);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_queue.IsEmpty)
                {
                    await Task.Delay(20, stoppingToken);
                    continue;
                }

                using var batch = await sender.CreateMessageBatchAsync(stoppingToken);
                var added = 0;

                while (added < batchSize && _queue.TryDequeue(out var body))
                {
                    var message = new ServiceBusMessage(body)
                    {
                        ContentType = "application/json",
                        MessageId = Guid.NewGuid().ToString()
                    };

                    if (!batch.TryAddMessage(message))
                    {
                        // Batch is full - return message to queue and break to send this batch
                        _queue.Enqueue(body);
                        break;
                    }

                    added++;
                }

                if (batch.Count > 0)
                {
                    await sender.SendMessagesAsync(batch, stoppingToken);
                }

                await Task.Yield();
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception occurred in the AzureServiceBusEventBatchBackgroundService");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        while (!_queue.IsEmpty)
        {
            using var batch = await sender.CreateMessageBatchAsync(cancellationToken);

            while (_queue.TryDequeue(out var body))
            {
                var msg = new ServiceBusMessage(body)
                {
                    ContentType = "application/json",
                    MessageId = Guid.NewGuid().ToString()
                };

                if (!batch.TryAddMessage(msg))
                {
                    _queue.Enqueue(body); // re-enqueue for next batch
                    break;
                }
            }

            if (batch.Count > 0)
                await sender.SendMessagesAsync(batch, cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}
