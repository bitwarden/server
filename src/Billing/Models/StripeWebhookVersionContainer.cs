using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class StripeWebhookVersionContainer
{
    [JsonPropertyName("api_version")]
    public string ApiVersion { get; set; }
}
