using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class StripeWebhookDeliveryContainer
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("api_version")]
    public string ApiVersion { get; set; }

    [JsonPropertyName("request")]
    public StripeWebhookRequestData Request { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }
}

public class StripeWebhookRequestData
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
}
