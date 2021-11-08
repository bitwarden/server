using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore
{
    public class SponsoredPlan
    {
        public PlanSponsorshipType PlanSponsorshipType { get; set; }
        public ProductType SponsoredProductType { get; set; }
        public ProductType SponsoringProductType { get; set; }
        public string StripePlanId { get; set; }
    }
}
