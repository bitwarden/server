using Azure.Storage.Queues;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Notifications;

public class AzureQueueHostedService : IHostedService, IDisposable
{
    private readonly ILogger _logger;
    private readonly HubHelpers _hubHelpers;
    private readonly GlobalSettings _globalSettings;

    private Task? _executingTask;
    private CancellationTokenSource? _cts;

    public AzureQueueHostedService(
        ILogger<AzureQueueHostedService> logger,
        HubHelpers hubHelpers,
        GlobalSettings globalSettings)
    {
        _logger = logger;
        _hubHelpers = hubHelpers;
        _globalSettings = globalSettings;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
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
        _cts?.Cancel();
        await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var queueClient = new QueueClient(_globalSettings.Notifications.ConnectionString, "notifications");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await queueClient.ReceiveMessagesAsync(32, cancellationToken: cancellationToken);
                if (messages.Value?.Any() ?? false)
                {
                    foreach (var message in messages.Value)
                    {
                        try
                        {
                            var decodedMessage = message.DecodeMessageText();
                            if (!string.IsNullOrWhiteSpace(decodedMessage))
                            {
                                await _hubHelpers.SendNotificationToHubAsync(decodedMessage, cancellationToken);
                            }

                            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt,
                                cancellationToken);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error processing dequeued message: {MessageId} x{DequeueCount}.",
                                message.MessageId, message.DequeueCount);
                            if (message.DequeueCount > 2)
                            {
                                await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt,
                                    cancellationToken);
                            }
                        }
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Task.Delay cancelled during Alpine container shutdown");
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing messages.");
            }
        }

        _logger.LogWarning("Done processing.");
    }
}
