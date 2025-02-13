using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IEventMessageHandler
{
    Task HandleEventAsync(EventMessage eventMessage);

    Task HandleManyEventAsync(IEnumerable<EventMessage> eventMessages);
}
