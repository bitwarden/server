using Bit.Core.Models.Table;

namespace Bit.Core.Models.Data
{
    public class PlanTypePlanTypeGroup
    {
        public int Id { get; set; }
        public string StripePlanId { get; set; }
        public string StripeSeatPlanId { get; set; }
        public string StripeStoragePlanId { get; set; }
        public string StripePremiumAcessPlanId { get; set; }
        public double BasePrice { get; set; }
        public double SeatPrice { get; set; }
        public double AdditionalStoragePricePerGb { get; set; }
        public double PremiumAccessAddonCost { get; set; }
        public bool IsAnnual { get; set; }
        public string PlanTypeGroupId { get; set; }
        public PlanTypeGroup PlanTypeGroup { get; set; }
    }
}
