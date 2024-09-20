using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bit.Admin.Billing.Models.ProcessStripeEvents;

public class EventIDsFormModel
{
    [Required]
    [JsonPropertyName("eventIds")]
    public List<string> EventIDs { get; set; } = [];
}
