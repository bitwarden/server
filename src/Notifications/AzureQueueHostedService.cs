using Azure.Storage.Queues;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Notifications;

public class AzureQueueHostedService : IHostedService, IDisposable
{
    private readonly ILogger _logger;
    private readonly HubHelpers _hubHelpers;
    private readonly GlobalSettings _globalSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;

    private Task? _executingTask;
    private CancellationTokenSource? _cts;

    public AzureQueueHostedService(
        ILogger<AzureQueueHostedService> logger,
        HubHelpers hubHelpers,
        GlobalSettings globalSettings,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _hubHelpers = hubHelpers;
        _globalSettings = globalSettings;
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_globalSettings.SelfHosted ||
            !CoreHelpers.SettingHasValue(_globalSettings.Notifications?.ConnectionString))
        {
            return Task.CompletedTask;
        }

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
        var queueClient = _serviceProvider.GetRequiredKeyedService<QueueClient>("notifications");

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
                            // CoreHelpers.DecodeMessageText inlined, so that a successful decode can
                            // be reported: nothing writes base64 to this queue any more, and the
                            // decode exists only to tolerate a sender that predates that. The warning
                            // is how we find out whether any still does, so the tolerance can be
                            // dropped on evidence rather than on the assumption that it is unused.
                            var decodedMessage = message.MessageText;
                            if (!string.IsNullOrWhiteSpace(decodedMessage))
                            {
                                try
                                {
                                    decodedMessage = CoreHelpers.Base64DecodeString(decodedMessage);
                                    _logger.LogWarning(
                                        "Dequeued a base64-encoded message: {MessageId}. Decoding it is legacy tolerance, not something a current sender needs.",
                                        message.MessageId);
                                }
                                catch
                                {
                                    // Not base64, so it is the plain text a current sender writes.
                                    // Catching everything is what CoreHelpers.DecodeMessageText does,
                                    // and this is only meant to inline it, not to change it.
                                }
                            }

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
                    await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, cancellationToken);
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
