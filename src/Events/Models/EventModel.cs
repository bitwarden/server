using Bit.Core.Enums;

namespace Bit.Events.Models;

public class EventModel
{
    public EventType Type { get; set; }
    public Guid? CipherId { get; set; }
    /// <summary>
    /// Accepted for backwards compatibility with existing clients and deliberately ignored.
    /// Events are stamped with server time in <see cref="Bit.Core.Services.IEventService"/>.
    /// </summary>
    public DateTime Date { get; set; }
    public Guid? OrganizationId { get; set; }
}
