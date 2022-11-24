using Azure.Storage.Queues;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public class AzureQueueHostedService : IHostedService, IDisposable
{
    private readonly ILogger<AzureQueueHostedService> _logger;
    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly IHubContext<AnonymousNotificationsHub> _anonymousHubContext;
    private readonly GlobalSettings _globalSettings;

    private Task _executingTask;
    private CancellationTokenSource _cts;
    private QueueClient _queueClient;

    public AzureQueueHostedService(
        ILogger<AzureQueueHostedService> logger,
        IHubContext<NotificationsHub> hubContext,
        IHubContext<AnonymousNotificationsHub> anonymousHubContext,
        GlobalSettings globalSettings)
    {
        _logger = logger;
        _hubContext = hubContext;
        _globalSettings = globalSettings;
        _anonymousHubContext = anonymousHubContext;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting service.");
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
        _queueClient = new QueueClient(_globalSettings.Notifications.ConnectionString, "notifications");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _queueClient.ReceiveMessagesAsync(32);
                if (messages.Value?.Any() ?? false)
                {
                    _logger.LogInformation("Retrieved {count} messages from queue", messages.Value.Count());
                    foreach (var message in messages.Value)
                    {
                        using (_logger.BeginScope(new Dictionary<string, object>
                        {
                            ["MessageId"] = message.MessageId,
                            ["DequeueCount"] = message.DequeueCount
                        }))
                        {
                            try
                            {
                                var decodedText = message.DecodeMessageText();
                                _logger.LogInformation("Processing message with text {message}", decodedText);
                                await HubHelpers.SendNotificationToHubAsync(
                                    decodedText, _hubContext, _anonymousHubContext, _logger, cancellationToken);
                                _logger.LogInformation("Completed processing of message", message.MessageId);
                                await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                                _logger.LogDebug("Deleted message from the queue");
                            }
                            catch (Exception e)
                            {
                                _logger.LogError("Error processing dequeued message: " +
                                    $"{message.MessageId} x{message.DequeueCount}. {e.Message}", e);
                                if (message.DequeueCount > 2)
                                {
                                    await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No notifications retrieved.  Waiting 5 seconds");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error processing messages.", e);
            }
        }

        _logger.LogWarning("Done processing.");
    }
}
