using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models;

/// <summary>
/// Provides static sponsored plan data.
/// </summary>
public static class SponsoredPlans
{
    /// <summary>
    /// The single Families for Enterprise sponsored plan.
    /// </summary>
    public static SponsoredPlan FamiliesForEnterprise { get; } = new SponsoredPlan
    {
        PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
        SponsoredProductTierType = ProductTierType.Families,
        SponsoringProductTierType = ProductTierType.Enterprise,
        StripePlanId = "2021-family-for-enterprise-annually",
        UsersCanSponsor = (OrganizationUserOrganizationDetails org) =>
            org.PlanType.GetProductTier() == ProductTierType.Enterprise,
    };
}
