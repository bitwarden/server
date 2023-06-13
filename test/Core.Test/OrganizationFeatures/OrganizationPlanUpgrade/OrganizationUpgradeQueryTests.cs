using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;
using Bit.Core.Repositories;
using Bit.Core.Models.StaticStore;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationPlanUpgrade;

[SutProviderCustomize]
public class OrganizationUpgradeQueryTests
{
    
    [Theory]
    [BitAutoData]
    public void ExistingPlan_ShouldReturnMatchingPlan(SutProvider<OrganizationUpgradeQuery> sutProvider)
    {
        const PlanType planType = PlanType.EnterpriseAnnually;
        var expectedPlan = new Plan { Type = planType };
        var plans = new List<Plan> { expectedPlan, new Plan { Type = PlanType.EnterpriseAnnually } };
        StaticStore.Plans = plans;

        var result = sutProvider.Sut.ExistingPlan(planType);

        Assert.Equal(expectedPlan, result);
    }

    [Theory]
    [BitAutoData]
    public void NewPlans_ShouldReturnMatchingPlans(SutProvider<OrganizationUpgradeQuery> sutProvider)
    {
        const PlanType planType = PlanType.EnterpriseAnnually;
        var matchingPlan = new Plan { Type = planType, Disabled = false };
        var disabledPlan = new Plan { Type = planType, Disabled = true };
        var plans = new List<Plan> { matchingPlan, disabledPlan };
        StaticStore.Plans = plans;

        var result = sutProvider.Sut.NewPlans(planType);

        Assert.Contains(matchingPlan, result);
        Assert.DoesNotContain(disabledPlan, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrgById_ShouldReturnOrganization(SutProvider<OrganizationUpgradeQuery> sutProvider)
    {
        var orgId = Guid.NewGuid();
        var expectedOrganization = new Organization { Id = orgId };
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(expectedOrganization);

        var result = await sutProvider.Sut.GetOrgById(orgId);

        Assert.Equal(expectedOrganization, result);
    }
}
