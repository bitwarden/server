using Bit.Core.Models.Data;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Dirt.Services.Implementations;

public class EventRepositoryHandler(
    [FromKeyedServices("persistent")] IEventWriteService eventWriteService)
    : IEventMessageHandler
{
    public Task HandleEventAsync(EventMessage eventMessage)
    {
        return eventWriteService.CreateAsync(eventMessage);
    }

    public Task HandleManyEventsAsync(IEnumerable<EventMessage> eventMessages)
    {
        return eventWriteService.CreateManyAsync(eventMessages);
    }
}
