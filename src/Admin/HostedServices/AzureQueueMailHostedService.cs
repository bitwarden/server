// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using System.Collections.Concurrent;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Bit.Core.Models.Mail;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Admin.HostedServices;

public record FailedMailMessage(MailQueueMessage Message, int RetryCount)
{
    public DateTime LastAttemptTime { get; init; } = default;
};

public class AzureQueueMailHostedService : IHostedService
{
    private readonly ILogger<AzureQueueMailHostedService> _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly IMailService _mailService;
    private CancellationTokenSource _cts;
    private Task _executingTask;

    private QueueClient _mailQueueClient;
    private readonly ConcurrentQueue<FailedMailMessage> _failedMessages = new();
    private readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(9) };
    private Task _failedMessageProcessingTask;

    public AzureQueueMailHostedService(
        ILogger<AzureQueueMailHostedService> logger,
        IMailService mailService,
        GlobalSettings globalSettings)
    {
        _logger = logger;
        _mailService = mailService;
        _globalSettings = globalSettings;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = ExecuteAsync(_cts.Token);
        _failedMessageProcessingTask = ProcessFailedMessagesBackgroundAsync(_cts.Token);
        return Task.WhenAny(_executingTask, _failedMessageProcessingTask).IsCompleted ?
               Task.WhenAll(_executingTask, _failedMessageProcessingTask) : Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
        {
            return;
        }
        _cts.Cancel();

        var tasksToWait = new List<Task> { _executingTask };
        if (_failedMessageProcessingTask != null)
        {
            tasksToWait.Add(_failedMessageProcessingTask);
        }

        await Task.WhenAny(Task.WhenAll(tasksToWait), Task.Delay(-1, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _mailQueueClient = new QueueClient(_globalSettings.Mail.ConnectionString, "mail");

        while (!cancellationToken.IsCancellationRequested)
        {
            var mailMessages = await RetrieveMessagesAsync();

            if (!mailMessages.Any())
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                continue;
            }

            // Process all messages concurrently
            var processingTasks = mailMessages.Select(message => ProcessMessageAsync(message, cancellationToken));
            await Task.WhenAll(processingTasks);
        }
    }

    private async Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(message.DecodeMessageText());
            var root = document.RootElement;

            var mailMessages = new List<MailQueueMessage>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                mailMessages.AddRange(root.Deserialize<List<MailQueueMessage>>());
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                mailMessages.Add(root.Deserialize<MailQueueMessage>());
            }

            // Try to send each individual mail message
            var failedMessages = new List<MailQueueMessage>();

            foreach (var mailMessage in mailMessages)
            {
                try
                {
                    await _mailService.SendEnqueuedMailMessageAsync(mailMessage);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to send individual email message. Will be re-enqueued for retry.");
                    failedMessages.Add(mailMessage);
                }
            }

            // If all messages succeeded, delete the original message
            if (!failedMessages.Any())
            {
                await _mailQueueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                _logger.LogInformation("Successfully processed all email messages in batch");
            }
            else
            {
                // Queue failed messages for re-enqueuing
                foreach (var failedMessage in failedMessages)
                {
                    _failedMessages.Enqueue(new FailedMailMessage(failedMessage, 0));
                }

                // Delete the original message since we've extracted individual failures
                await _mailQueueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                _logger.LogInformation("Processed batch with {SuccessCount} successful and {FailedCount} failed messages",
                    mailMessages.Count - failedMessages.Count, failedMessages.Count);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to parse or process queue message. Message will be left in queue for retry.");
        }
    }

    private async Task ProcessFailedMessagesBackgroundAsync(CancellationToken cancellationToken)
    {
        const int maxRetryAttempts = 3;
        const int pollingIntervalSeconds = 5;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messagesToRequeue = new List<FailedMailMessage>();
                var processedCount = 0;

                // Process failed messages in batches to avoid overwhelming the system
                while (_failedMessages.TryDequeue(out var failedMessage) && processedCount < 10)
                {
                    processedCount++;

                    // Calculate when this message should be retried based on its retry count
                    var nextRetryTime = CalculateNextRetryTime(failedMessage);

                    if (DateTime.UtcNow < nextRetryTime)
                    {
                        // Message is not ready for retry yet, put it back for later
                        messagesToRequeue.Add(failedMessage);
                        continue;
                    }

                    try
                    {
                        await _mailService.SendEnqueuedMailMessageAsync(failedMessage.Message);
                        _logger.LogInformation("Successfully sent previously failed email message on retry attempt {RetryCount}",
                            failedMessage.RetryCount + 1);
                    }
                    catch (Exception e)
                    {
                        var newRetryCount = failedMessage.RetryCount + 1;

                        if (newRetryCount < maxRetryAttempts)
                        {
                            _logger.LogWarning(e, "Failed to send email on retry attempt {RetryCount}/{MaxRetryAttempts}. Will retry later.",
                                newRetryCount, maxRetryAttempts);
                            messagesToRequeue.Add(new FailedMailMessage(failedMessage.Message, newRetryCount)
                            {
                                LastAttemptTime = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            _logger.LogError(e, "Failed to send email after {MaxRetryAttempts} retry attempts. Message will be permanently discarded.",
                                maxRetryAttempts);
                        }
                    }
                }

                // Put messages that need more processing back into the queue
                foreach (var messageToRequeue in messagesToRequeue)
                {
                    _failedMessages.Enqueue(messageToRequeue);
                }

                if (processedCount > 0)
                {
                    _logger.LogInformation("Background processor handled {ProcessedCount} failed messages", processedCount);
                }

                // Wait before next polling cycle
                await Task.Delay(TimeSpan.FromSeconds(pollingIntervalSeconds), cancellationToken);
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "Error in background failed message processor. Will retry in {IntervalSeconds}s.", pollingIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(pollingIntervalSeconds), cancellationToken);
            }
        }
    }

    private DateTime CalculateNextRetryTime(FailedMailMessage failedMessage)
    {
        if (failedMessage.RetryCount == 0 || failedMessage.LastAttemptTime == default)
        {
            return DateTime.UtcNow; // First attempt or no previous attempt time
        }

        var delayIndex = Math.Min(failedMessage.RetryCount - 1, RetryDelays.Length - 1);
        var delay = RetryDelays[delayIndex];
        return failedMessage.LastAttemptTime.Add(delay);
    }

    private async Task<QueueMessage[]> RetrieveMessagesAsync()
    {
        return (await _mailQueueClient.ReceiveMessagesAsync(maxMessages: 32))?.Value ?? new QueueMessage[] { };
    }
}
