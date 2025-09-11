using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class FreshdeskReplyRequestModel
{
    [JsonPropertyName("body")]
    public required string Body { get; set; }
}
