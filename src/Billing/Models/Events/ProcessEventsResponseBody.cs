namespace Bit.Billing.Models.Events;

public class ProcessEventsResponseBody
{
    public List<EventResponseBody> Successful { get; set; }
    public List<EventResponseBody> Failed { get; set; }
}
