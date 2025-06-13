#nullable enable

using System.Text.Json;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;
public class EventIntegrationEventWriteService : IEventWriteService, IAsyncDisposable
{
    private readonly IEventIntegrationPublisher _eventIntegrationPublisher;

    public EventIntegrationEventWriteService(IEventIntegrationPublisher eventIntegrationPublisher)
    {
        _eventIntegrationPublisher = eventIntegrationPublisher;
    }

    public async Task CreateAsync(IEvent e)
    {
        var body = JsonSerializer.Serialize(e);
        await _eventIntegrationPublisher.PublishEventAsync(body: body);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> events)
    {
        var body = JsonSerializer.Serialize(events);
        await _eventIntegrationPublisher.PublishEventAsync(body: body);
    }

    public async ValueTask DisposeAsync()
    {
        await _eventIntegrationPublisher.DisposeAsync();
    }
}
