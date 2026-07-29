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

    [Fact]
    public void EveryEligibleSourcePlan_IsBilledPerSeat()
    {
        // The eligibility set is read from production here, not restated by hand: it is every
        // PlanType that ResolveAnnualLatestPlanType actually accepts. The offer maps line items
        // one for one onto annual prices, so it only works for plans billed per seat. A Packaged
        // plan charges one flat fee covering several seats, which has no per-unit annual
        // equivalent, and PlanAdapter marks that shape by populating StripePlanId. If a future
        // change adds a Packaged vintage to AnnualUpgradeOfferPlans.MonthlyToAnnualLatest, this
        // test fails and names it, rather than quoting a wrong figure to a customer.
        var eligiblePlanTypes = Enum.GetValues<PlanType>()
            .Where(planType => AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(planType) is not null)
            .ToList();

        Assert.NotEmpty(eligiblePlanTypes);

        var packagedOffenders = eligiblePlanTypes
            .Where(planType => !string.IsNullOrEmpty(MockPlans.Get(planType).PasswordManager.StripePlanId))
            .ToList();

        Assert.True(packagedOffenders.Count == 0,
            $"Packaged plan(s) leaked into the eligibility set: {string.Join(", ", packagedOffenders)}");
    }

    [Fact]
    public void PackagedTeams2019_IsNotEligible()
    {
        Assert.Null(AnnualUpgradeOfferPlans.ResolveAnnualLatestPlanType(PlanType.TeamsMonthly2019));
        Assert.False(string.IsNullOrEmpty(
            MockPlans.Get(PlanType.TeamsMonthly2019).PasswordManager.StripePlanId));
    }
}
