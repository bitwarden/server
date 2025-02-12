using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Services.Implementations;

public class AzureServiceBusEventWriteService : IEventWriteService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public AzureServiceBusEventWriteService(GlobalSettings globalSettings)
    {
        _client = new ServiceBusClient(globalSettings.EventLogging.AzureServiceBus.ConnectionString);
        _sender = _client.CreateSender(globalSettings.EventLogging.AzureServiceBus.TopicName);
    }

    public async Task CreateAsync(IEvent e)
    {
        var message = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(e))
        {
            ContentType = "application/json"
        };

        await _sender.SendMessageAsync(message);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> events)
    {
        foreach (var e in events)
        {
            await CreateAsync(e);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
