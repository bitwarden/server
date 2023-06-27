using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSubscriptionUpdateRequestModel
{
    [Required]
    public int SeatAdjustment { get; set; }
    public int? MaxAutoscaleSeats { get; set; }
}
