using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Services.UpgradeOrganizationPlan.Queries;

public class GetPlanByTypeQuery
{
    public static Plan Execute(PlanType planType)
    {
        return  StaticStore.Plans.FirstOrDefault(p => p.Type == planType);
    }
}
