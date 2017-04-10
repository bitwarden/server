using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore
{
    public class Plan
    {
        public string Name { get; set; }
        public string StripePlanId { get; set; }
        public string StripeUserPlanId { get; set; }
        public PlanType Type { get; set; }
        public short BaseUsers { get; set; }
        public bool CanBuyAdditionalUsers { get; set; }
        public short? MaxAdditionalUsers { get; set; }
        public decimal BasePrice { get; set; }
        public decimal UserPrice { get; set; }
        public short? MaxSubvaults { get; set; }
        public int UpgradeSortOrder { get; set; }
        public bool Disabled { get; set; }
    }
}
