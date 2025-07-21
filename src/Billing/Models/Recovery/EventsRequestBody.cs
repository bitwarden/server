// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;

namespace Bit.Billing.Models.Recovery;

public class EventsRequestBody
{
    [JsonPropertyName("eventIds")]
    public List<string> EventIds { get; set; }
}
