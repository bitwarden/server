using System.Text.Json.Serialization;

namespace Bit.Billing.Models.Recovery;

public class EventsRequestBody
{
    [JsonPropertyName("eventIds")]
    public List<string> EventIds { get; set; }
}
