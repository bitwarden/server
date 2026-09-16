using Azure.Messaging.ServiceBus;
using Bit.Core.Dirt.Services;
using Bit.Core.Settings;

namespace Bit.EventsProcessor;

public class DeadLetterCleanupHostedService : BackgroundService
{
    internal const int BatchSize = 32;

    private static readonly TimeSpan _receiveWaitTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _sweepInterval = TimeSpan.FromHours(1);

    private readonly ILogger<DeadLetterCleanupHostedService> _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly IAzureServiceBusService _serviceBusService;
    private readonly TimeProvider _timeProvider;

    public DeadLetterCleanupHostedService(
        ILogger<DeadLetterCleanupHostedService> logger,
        GlobalSettings globalSettings,
        IAzureServiceBusService serviceBusService,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _globalSettings = globalSettings;
        _serviceBusService = serviceBusService;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retention = _globalSettings.EventLogging.AzureServiceBus.DeadLetterRetention;
        if (retention <= TimeSpan.Zero)
        {
            _logger.LogInformation("Dead letter cleanup is disabled. No retention configured.");
            return;
        }

        var topicName = _globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName;
        var subscriptionNames = IntegrationSubscriptionNames(_globalSettings).ToList();

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var subscriptionName in subscriptionNames)
            {
                // Isolated per subscription so one unreachable subscription cannot stop the others from being swept
                try
                {
                    await CleanUpSubscriptionAsync(topicName, subscriptionName, retention, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred sweeping dead letter queue for {Subscription}.", subscriptionName);
                }
            }

            try
            {
                await Task.Delay(_sweepInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CleanUpSubscriptionAsync(
        string topicName,
        string subscriptionName,
        TimeSpan retention,
        CancellationToken cancellationToken)
    {
        var cutoff = _timeProvider.GetUtcNow().Subtract(retention);
        await using var receiver = _serviceBusService.CreateDeadLetterReceiver(topicName, subscriptionName);

        var deleted = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await ProcessBatchAsync(receiver, cutoff, cancellationToken);
            deleted += result.Deleted;

            if (!result.ContinueSweep)
            {
                break;
            }
        }

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Deleted {Deleted} dead-lettered messages older than {Cutoff} from {Subscription}.",
                deleted,
                cutoff,
                subscriptionName);
        }
    }

    internal static async Task<(int Deleted, bool ContinueSweep)> ProcessBatchAsync(
        ServiceBusReceiver receiver,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var messages = await receiver.ReceiveMessagesAsync(BatchSize, _receiveWaitTime, cancellationToken);
        if (messages is null || messages.Count == 0)
        {
            return (0, false);
        }

        var deleted = 0;
        foreach (var message in messages)
        {
            // EnqueuedTime is the age proxy; dead-lettering happens within seconds of the original publish
            if (message.EnqueuedTime >= cutoff)
            {
                // Dead letters are ordered oldest first, so everything past this point is within retention
                await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                return (deleted, false);
            }

            await receiver.CompleteMessageAsync(message, cancellationToken);
            deleted++;
        }

        return (deleted, messages.Count == BatchSize);
    }

    private static IEnumerable<string> IntegrationSubscriptionNames(GlobalSettings globalSettings)
    {
        var settings = globalSettings.EventLogging.AzureServiceBus;

        yield return settings.SlackIntegrationSubscriptionName;
        yield return settings.WebhookIntegrationSubscriptionName;
        yield return settings.HecIntegrationSubscriptionName;
        yield return settings.DatadogIntegrationSubscriptionName;
        yield return settings.TeamsIntegrationSubscriptionName;
    }
}
