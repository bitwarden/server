using System.Text.Json.Serialization;

namespace Bit.Billing.Models.Events;

public class InspectEventsResponseBody
{
    [JsonPropertyName("events")]
    public IEnumerable<EventResponseBody> Events { get; set; }
}
