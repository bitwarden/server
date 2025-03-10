using System.Text.Json.Serialization;

namespace Bit.Billing.Models.Recovery;

public class EventsResponseBody
{
    [JsonPropertyName("events")]
    public List<EventResponseBody> Events { get; set; }
}

public class EventResponseBody
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("url")]
    public string URL { get; set; }

    [JsonPropertyName("apiVersion")]
    public string APIVersion { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("createdUTC")]
    public DateTime CreatedUTC { get; set; }

    [JsonPropertyName("processingError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ProcessingError { get; set; }
}
