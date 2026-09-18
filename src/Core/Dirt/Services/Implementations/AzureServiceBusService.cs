using Azure.Messaging.ServiceBus;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Settings;

namespace Bit.Core.Dirt.Services.Implementations;

public class AzureServiceBusService : IAzureServiceBusService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _eventSender;
    private readonly ServiceBusSender _integrationSender;
    private readonly TimeSpan _integrationMessageTimeToLive;

    public AzureServiceBusService(GlobalSettings globalSettings)
    {
        _client = new ServiceBusClient(globalSettings.EventLogging.AzureServiceBus.ConnectionString);
        _eventSender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.EventTopicName);
        _integrationSender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName);
        _integrationMessageTimeToLive = globalSettings.EventLogging.AzureServiceBus.IntegrationMessageTimeToLive;
    }

    public ServiceBusProcessor CreateProcessor(string topicName, string subscriptionName, ServiceBusProcessorOptions options)
    {
        return _client.CreateProcessor(topicName, subscriptionName, options);
    }

    public ServiceBusReceiver CreateDeadLetterReceiver(string topicName, string subscriptionName)
    {
        return _client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });
    }

    public async Task PublishAsync(IIntegrationMessage message)
    {
        await _integrationSender.SendMessageAsync(
            BuildIntegrationMessage(message, _integrationMessageTimeToLive));
    }

    public async Task PublishToRetryAsync(IIntegrationMessage message)
    {
        await _integrationSender.SendMessageAsync(
            BuildIntegrationMessage(
                message,
                _integrationMessageTimeToLive,
                scheduledEnqueueTime: message.DelayUntilDate ?? DateTime.UtcNow));
    }

    internal static ServiceBusMessage BuildIntegrationMessage(
        IIntegrationMessage message,
        TimeSpan timeToLive,
        DateTime? scheduledEnqueueTime = null)
    {
        var serviceBusMessage = new ServiceBusMessage(message.ToJson())
        {
            Subject = message.IntegrationType.ToRoutingKey(),
            MessageId = message.MessageId,
            PartitionKey = message.OrganizationId
        };

        if (timeToLive > TimeSpan.Zero)
        {
            serviceBusMessage.TimeToLive = timeToLive;
        }

        if (scheduledEnqueueTime.HasValue)
        {
            serviceBusMessage.ScheduledEnqueueTime = scheduledEnqueueTime.Value;
        }

        return serviceBusMessage;
    }

    public async Task PublishEventAsync(string body, string? organizationId)
    {
        var message = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            PartitionKey = organizationId
        };

        await _eventSender.SendMessageAsync(message);
    }

    public async ValueTask DisposeAsync()
    {
        await _eventSender.DisposeAsync();
        await _integrationSender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
