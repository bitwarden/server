using Bit.Core.Models.Data;

namespace Bit.Core.Dirt.Services;

public interface IEventMessageHandler
{
    Task HandleEventAsync(EventMessage eventMessage);

    Task HandleManyEventsAsync(IEnumerable<EventMessage> eventMessages);
}
