using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Models.StaticStore;

public class SponsoredPlan
{
    public PlanSponsorshipType PlanSponsorshipType { get; set; }
    public ProductTierType SponsoredProductType { get; set; }
    public ProductTierType SponsoringProductType { get; set; }
    public string StripePlanId { get; set; }
    public Func<OrganizationUserOrganizationDetails, bool> UsersCanSponsor { get; set; }
}
