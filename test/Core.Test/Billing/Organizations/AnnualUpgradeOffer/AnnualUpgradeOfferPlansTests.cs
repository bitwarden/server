using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Test.Billing.Mocks;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer;

public class AnnualUpgradeOfferPlansTests
{
    [Theory]
    [InlineData(PlanType.TeamsMonthly, PlanType.TeamsAnnually)]
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
    [InlineData(PlanType.TeamsMonthly2019)]
    public void ResolveAnnualLatestPlanType_NotAMonthlyBusinessPlan_ReturnsNull(PlanType current)
    {
        var result = AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(current);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(PlanType.TeamsMonthly2020)]
    [InlineData(PlanType.TeamsMonthly2023)]
    [InlineData(PlanType.TeamsMonthly)]
    [InlineData(PlanType.EnterpriseMonthly2019)]
    [InlineData(PlanType.EnterpriseMonthly2020)]
    [InlineData(PlanType.EnterpriseMonthly2023)]
    [InlineData(PlanType.EnterpriseMonthly)]
    public void EveryEligibleSourcePlan_IsBilledPerSeat(PlanType planType)
    {
        // The offer maps line items one for one onto annual prices, so it only works for plans
        // billed per seat. A Packaged plan charges one flat fee covering several seats, which has
        // no per-unit annual equivalent, and PlanAdapter marks that shape by populating
        // StripePlanId. TeamsMonthly2019 is absent from the eligibility list for exactly this
        // reason; this test makes adding another Packaged vintage fail here rather than quote a
        // wrong figure to a customer.
        var plan = MockPlans.Get(planType);

        Assert.True(string.IsNullOrEmpty(plan.PasswordManager.StripePlanId));
    }

    [Fact]
    public void PackagedTeams2019_IsNotEligible()
    {
        Assert.Null(AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(PlanType.TeamsMonthly2019));
        Assert.False(string.IsNullOrEmpty(
            MockPlans.Get(PlanType.TeamsMonthly2019).PasswordManager.StripePlanId));
    }
}
