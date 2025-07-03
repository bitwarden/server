#nullable enable

using Bit.Core.Models.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Services;

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
