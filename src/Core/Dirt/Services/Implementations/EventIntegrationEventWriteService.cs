using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Services;

namespace Bit.Core.Dirt.Services.Implementations;
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
        await _eventIntegrationPublisher.PublishEventAsync(body: body, organizationId: e.OrganizationId?.ToString());
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> events)
    {
        var eventList = events as IList<IEvent> ?? events.ToList();
        if (eventList.Count == 0)
        {
            return;
        }

        var organizationId = eventList[0].OrganizationId?.ToString();
        var body = JsonSerializer.Serialize(eventList);
        await _eventIntegrationPublisher.PublishEventAsync(body: body, organizationId: organizationId);
    }
    public async ValueTask DisposeAsync()
    {
        await _eventIntegrationPublisher.DisposeAsync();
    }
}
