using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class ProcessEventsRequestBody
{
    [JsonPropertyName("apiVersion")]
    public string APIVersion { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool DeliverySuccess { get; set; }
    public string Region { get; set; }
}
