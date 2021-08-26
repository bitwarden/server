using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationSeatAutoscaleRequestModel
    {
        [Required]
        public bool EnableSeatAutoscaling { get; set; }
        public int? MaxAutoscaleSeats { get; set; }
    }
}
