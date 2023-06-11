using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;

public interface IGetPlanByTypeQuery
{
    Plan ExistingPlan(PlanType planType);

    List<Plan> NewPlans(PlanType planType);

    Task<Organization> GetOrgById(Guid id);
}
