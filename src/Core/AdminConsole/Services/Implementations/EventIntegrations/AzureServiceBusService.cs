using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureServiceBusService : IAzureServiceBusService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _eventSender;
    private readonly ServiceBusSender _integrationSender;

    private readonly AzureServiceBusEventBatchBackgroundService _backgroundEventWorker;
    private readonly CancellationTokenSource _cts = new();

    public AzureServiceBusService(GlobalSettings globalSettings, ILogger<AzureServiceBusService> logger)
    {
        _client = new ServiceBusClient(globalSettings.EventLogging.AzureServiceBus.ConnectionString);
        _eventSender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.EventTopicName);
        _integrationSender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName);

        _backgroundEventWorker = new AzureServiceBusEventBatchBackgroundService(
            sender: _eventSender,
            batchSize: globalSettings.EventLogging.AzureServiceBus.SenderBatchSize,
            logger: logger);
        _ = _backgroundEventWorker.StartAsync(_cts.Token);
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
        _backgroundEventWorker.Enqueue(body);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _backgroundEventWorker.StopAsync(CancellationToken.None);
        await _eventSender.DisposeAsync();
        await _integrationSender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
