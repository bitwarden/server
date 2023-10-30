using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Public.Models;

public class OrganizationSubscriptionUpdateRequestModel
{
    [Required]
    public int SeatAdjustment { get; set; }
    public int? MaxAutoscaleSeats { get; set; }
}
