using System.Text.Json.Serialization;

namespace Bit.Admin.Billing.Models.ProcessStripeEvents;

public class EventsRequestBody
{
    [JsonPropertyName("eventIds")]
    public List<string> EventIds { get; set; }
}
