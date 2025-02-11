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
}
