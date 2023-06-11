using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;

public class GetPlanByTypeQuery : IGetPlanByTypeQuery
{
    public Plan ExistingPlan(PlanType planType)
    { 
        return  StaticStore.Plans.FirstOrDefault(p => p.Type == planType);
    }

    public List<Plan> NewPlans(PlanType planType)
    {
        return  StaticStore.Plans.Where(p => p.Type == planType && !p.Disabled).ToList();
    }
}
