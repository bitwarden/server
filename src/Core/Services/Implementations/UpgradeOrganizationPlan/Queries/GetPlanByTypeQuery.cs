using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.Utilities;

namespace Bit.Core.Services.Implementations.UpgradeOrganizationPlan.Queries;

public static class GetPlanByTypeQuery
{
    public static Plan  ExistingPlan(PlanType planType)
    {
        return  StaticStore.Plans.FirstOrDefault(p => p.Type == planType);
    }
    
    public static List<Plan>  NewPlans(PlanType planType)
    {
        return  StaticStore.Plans.Where(p => p.Type == planType && !p.Disabled).ToList();
    }
}
