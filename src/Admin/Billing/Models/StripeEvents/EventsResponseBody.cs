using System.Text.Json.Serialization;

namespace Bit.Admin.Billing.Models.StripeEvents;

public class EventsResponseBody
{
    [JsonPropertyName("events")]
    public IEnumerable<EventResponseBody> Events { get; set; }

    [JsonIgnore]
    public EventActionType ActionType { get; set; }
}

public enum EventActionType
{
    Inspect,
    Process
}
