using Stripe;

namespace Bit.Billing.Models;

public class ProcessedEventsResponseBody
{
    public List<ProcessedEvent> Success { get; set; }

    public List<ProcessedEvent> Failed { get; set; }
}

public class ProcessedEvent
{
    public string Id { get; set; }
    public string Type { get; set; }
    public DateTime Created { get; set; }
    public string Error { get; set; }

    public static ProcessedEvent From(Event stripeEvent, string error = null) => new ()
    {
        Id = stripeEvent.Id,
        Type = stripeEvent.Type,
        Created = stripeEvent.Created,
        Error = error
    };
}
