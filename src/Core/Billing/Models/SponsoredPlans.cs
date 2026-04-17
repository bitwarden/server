using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models;

public class SponsoredPlans
{
    public static IEnumerable<SponsoredPlan> All { get; set; } =
    [
        new()
        {
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            SponsoredProductTierType = ProductTierType.Families,
            SponsoringProductTierType = ProductTierType.Enterprise,
            StripePlanId = "2021-family-for-enterprise-annually",
            UsersCanSponsor = org =>
                org.PlanType.GetProductTier() == ProductTierType.Enterprise,
        }
    ];

    public static SponsoredPlan Get(PlanSponsorshipType planSponsorshipType) =>
        All.FirstOrDefault(p => p.PlanSponsorshipType == planSponsorshipType)!;
}
