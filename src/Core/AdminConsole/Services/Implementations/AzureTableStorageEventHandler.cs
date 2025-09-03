#nullable enable

using Bit.Core.Models.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Services;

public class AzureTableStorageEventHandler(
    [FromKeyedServices("persistent")] IEventWriteService eventWriteService)
    : IEventMessageHandler
{
    public Task HandleEventAsync(EventMessage eventMessage)
    {
        return eventWriteService.CreateManyAsync(EventTableEntity.IndexEvent(eventMessage));
    }

    public Task HandleManyEventsAsync(IEnumerable<EventMessage> eventMessages)
    {
        return eventWriteService.CreateManyAsync(eventMessages.SelectMany(EventTableEntity.IndexEvent));
    }
}
