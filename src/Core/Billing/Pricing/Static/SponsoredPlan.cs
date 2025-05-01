﻿using Bit.Core.Billing.Pricing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Billing.Pricing.Static;

public class SponsoredPlan
{
    public PlanSponsorshipType PlanSponsorshipType { get; set; }
    public ProductTierType SponsoredProductTierType { get; set; }
    public ProductTierType SponsoringProductTierType { get; set; }
    public string StripePlanId { get; set; }
    public Func<OrganizationUserOrganizationDetails, bool> UsersCanSponsor { get; set; }
}
