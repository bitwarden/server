using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;

public class OrganizationUpgradeQuery : IOrganizationUpgradeQuery
{
    private readonly IOrganizationRepository _organizationRepository;
    public OrganizationUpgradeQuery(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }
    public Plan ExistingPlan(PlanType planType)
    {
        return StaticStore.Plans.FirstOrDefault(p => p.Type == planType);
    }

    public List<Plan> NewPlans(PlanType planType)
    {
        return StaticStore.Plans.Where(p => p.Type == planType && !p.Disabled).ToList();
    }

    public async Task<Organization> GetOrgById(Guid id)
    {
        return await _organizationRepository.GetByIdAsync(id);
    }
}
