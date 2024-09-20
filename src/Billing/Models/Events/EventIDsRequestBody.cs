using System.Text.Json.Serialization;

namespace Bit.Billing.Models.Events;

public class EventIDsRequestBody
{
    [JsonPropertyName("eventIds")]
    public List<string> EventIDs { get; set; }
}
