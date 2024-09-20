using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class EventIDsRequestBody
{
    [JsonPropertyName("eventIds")]
    public List<string> EventIDs { get; set; }
}
