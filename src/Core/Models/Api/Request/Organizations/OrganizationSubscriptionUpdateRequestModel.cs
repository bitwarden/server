using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationSubscriptionUpdateRequestModel
    {
        [Required]
        public int SeatAdjustment { get; set; }
        public int? MaxAutoscaleSeats { get; set; }
    }
}
