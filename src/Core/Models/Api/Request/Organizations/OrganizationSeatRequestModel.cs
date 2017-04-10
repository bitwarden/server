using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationSeatRequestModel
    {
        [Required]
        public int? SeatAdjustment { get; set; }
    }
}
