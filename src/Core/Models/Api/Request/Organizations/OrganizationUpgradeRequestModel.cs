using Bit.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationUpgradeRequestModel
    {
        public PlanType PlanType { get; set; }
        [Range(0, double.MaxValue)]
        public short AdditionalSeats { get; set; }
    }
}
