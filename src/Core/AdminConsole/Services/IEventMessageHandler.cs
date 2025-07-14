// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IEventMessageHandler
{
    Task HandleEventAsync(EventMessage eventMessage);

    Task HandleManyEventsAsync(IEnumerable<EventMessage> eventMessages);
}
