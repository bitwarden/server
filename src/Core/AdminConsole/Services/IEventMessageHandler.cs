using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IEventMessageHandler
{
    Task HandleEventAsync(EventMessage eventMessage);

    Task HandleManyEventsAsync(IEnumerable<EventMessage> eventMessages);
}
