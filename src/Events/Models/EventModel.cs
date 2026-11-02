using Bit.Core.Enums;

namespace Bit.Events.Models;

public class EventModel
{
    public EventType Type { get; set; }
    public Guid? CipherId { get; set; }
    public DateTime Date { get; set; }
    public Guid? OrganizationId { get; set; }
}
