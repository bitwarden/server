using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer;

public class AnnualUpgradeOfferPlansTests
{
    [Theory]
    [InlineData(PlanType.TeamsMonthly, PlanType.TeamsAnnually)]
    [InlineData(PlanType.TeamsMonthly2019, PlanType.TeamsAnnually)]
    [InlineData(PlanType.TeamsMonthly2020, PlanType.TeamsAnnually)]
    [InlineData(PlanType.TeamsMonthly2023, PlanType.TeamsAnnually)]
    [InlineData(PlanType.EnterpriseMonthly, PlanType.EnterpriseAnnually)]
    [InlineData(PlanType.EnterpriseMonthly2019, PlanType.EnterpriseAnnually)]
    [InlineData(PlanType.EnterpriseMonthly2020, PlanType.EnterpriseAnnually)]
    [InlineData(PlanType.EnterpriseMonthly2023, PlanType.EnterpriseAnnually)]
    public void ResolveAnnualLatestPlanType_MonthlyBusinessPlan_ReturnsAnnualLatest(PlanType current, PlanType expected)
    {
        var result = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(current);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(PlanType.TeamsAnnually)]
    [InlineData(PlanType.EnterpriseAnnually)]
    [InlineData(PlanType.Free)]
    [InlineData(PlanType.FamiliesAnnually)]
    [InlineData(PlanType.TeamsStarter)]
    public void ResolveAnnualLatestPlanType_NotAMonthlyBusinessPlan_ReturnsNull(PlanType current)
    {
        var result = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(current);

        Assert.Null(result);
    }
}
