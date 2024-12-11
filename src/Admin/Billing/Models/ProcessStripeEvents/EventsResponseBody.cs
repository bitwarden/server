using System.Text.Json.Serialization;

namespace Bit.Admin.Billing.Models.ProcessStripeEvents;

public class EventsResponseBody
{
    [JsonPropertyName("events")]
    public List<EventResponseBody> Events { get; set; }

    [JsonIgnore]
    public EventActionType ActionType { get; set; }
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
    public string ProcessingError { get; set; }
}

public enum EventActionType
{
    Inspect,
    Process,
}
