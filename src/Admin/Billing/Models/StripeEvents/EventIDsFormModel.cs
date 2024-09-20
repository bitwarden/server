using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bit.Admin.Billing.Models.StripeEvents;

public class EventIDsFormModel
{
    [Required]
    [JsonPropertyName("eventIds")]
    public string EventIDs { get; set; }

    [Required]
    [JsonPropertyName("inspectOnly")]
    [DisplayName("Inspect Only")]
    public bool InspectOnly { get; set; }
}
