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

    public AzureServiceBusService(GlobalSettings globalSettings)
    {
        _client = new ServiceBusClient(globalSettings.EventLogging.AzureServiceBus.ConnectionString);
        _eventSender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.EventTopicName);
        _integrationSender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName);
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

    public async Task PublishEventAsync(string body)
    {
        var message = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString()
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
