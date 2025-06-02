using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Models.Data.Integrations;
using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class AzureServiceBusIntegrationPublisher : IIntegrationPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public AzureServiceBusIntegrationPublisher(GlobalSettings globalSettings)
    {
        _client = new ServiceBusClient(globalSettings.EventLogging.AzureServiceBus.ConnectionString);
        _sender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName);
    }

    public async Task PublishAsync(IIntegrationMessage message)
    {
        var json = message.ToJson();

        var serviceBusMessage = new ServiceBusMessage(json)
        {
            Subject = message.IntegrationType.ToRoutingKey(),
        };

        await _sender.SendMessageAsync(serviceBusMessage);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
