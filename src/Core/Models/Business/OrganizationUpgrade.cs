using Bit.Core.Enums;

namespace Bit.Core.Models.Business
{
    public class OrganizationUpgrade
    {
        public string BusinessName { get; set; }
        public PlanType Plan { get; set; }
        public short AdditionalSeats { get; set; }
        public short AdditionalStorageGb { get; set; }
        public bool PremiumAccessAddon { get; set; }
    }
}
