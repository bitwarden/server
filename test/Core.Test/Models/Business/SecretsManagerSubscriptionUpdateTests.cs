using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Models.Business;

[SecretsManagerOrganizationCustomize]
public class SecretsManagerSubscriptionUpdateTests
{
    private static TheoryData<Plan> ToPlanTheory(List<PlanType> types)
    {
        var theoryData = new TheoryData<Plan>();
        var plans = types.Select(MockPlans.Get).ToArray();
        theoryData.AddRange(plans);
        return theoryData;
    }

    public static TheoryData<Plan> NonSmPlans =>
        ToPlanTheory([PlanType.Custom, PlanType.FamiliesAnnually, PlanType.FamiliesAnnually2025, PlanType.FamiliesAnnually2019]);

    public static TheoryData<Plan> SmPlans => ToPlanTheory([
        PlanType.EnterpriseAnnually2019,
        PlanType.EnterpriseAnnually,
        PlanType.TeamsMonthly2019,
        PlanType.TeamsAnnually2020,
        PlanType.TeamsMonthly,
        PlanType.TeamsAnnually2019,
        PlanType.TeamsAnnually2020,
        PlanType.TeamsAnnually,
        PlanType.TeamsStarter
    ]);

    [Theory]
    [BitMemberAutoData(nameof(NonSmPlans))]
    public Task UpdateSubscriptionAsync_WithNonSecretsManagerPlanType_ThrowsBadRequestException(
        Plan plan,
        Organization organization)
    {
        // Arrange
        organization.PlanType = plan.Type;

        // Act
        var exception = Assert.Throws<NotFoundException>(() => new SecretsManagerSubscriptionUpdate(organization, plan, false));

        // Assert
        Assert.Contains("Invalid Secrets Manager plan", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        return Task.CompletedTask;
    }

    [Theory]
    [BitMemberAutoData(nameof(SmPlans))]
    public void UpdateSubscription_WithNonSecretsManagerPlanType_DoesNotThrowException(
        Plan plan,
        Organization organization)
    {
        // Arrange
        organization.PlanType = plan.Type;

        // Act
        var ex = Record.Exception(() => new SecretsManagerSubscriptionUpdate(organization, plan, false));

        // Assert
        Assert.Null(ex);
    }

    // PM-37510: the SmServiceAccountsExcludingBase clamp prevents a negative billed quantity when the
    // service-account count is at or below the (base + grace) ceiling.

    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_AtBaseline_WithNoGrace_ReturnsZero(Organization organization)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually); // BaseServiceAccount = 50
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmServiceAccounts = plan.SecretsManager.BaseServiceAccount
        };

        // Act / Assert
        Assert.Equal(0, sut.SmServiceAccountsExcludingBase);
    }

    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_BelowBaseline_WithNoGrace_ClampsToZero(Organization organization)
    {
        // Arrange — regression for the new Math.Max(0, ...) clamp. Without it this would be negative.
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually); // BaseServiceAccount = 50
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmServiceAccounts = plan.SecretsManager.BaseServiceAccount - 10
        };

        // Act / Assert
        Assert.Equal(0, sut.SmServiceAccountsExcludingBase);
    }

    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_AtBaselinePlusGrace_ReturnsZero(Organization organization)
    {
        // Arrange — migrated Enterprise org: base 50, grace 150 => free ceiling of 200.
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            ServiceAccountGrace = 150,
            SmServiceAccounts = plan.SecretsManager.BaseServiceAccount + 150
        };

        // Act / Assert
        Assert.Equal(0, sut.SmServiceAccountsExcludingBase);
    }

    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_BelowBaselinePlusGrace_ReturnsZero(Organization organization)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            ServiceAccountGrace = 150,
            SmServiceAccounts = plan.SecretsManager.BaseServiceAccount + 100
        };

        // Act / Assert
        Assert.Equal(0, sut.SmServiceAccountsExcludingBase);
    }

    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_AboveBaselinePlusGrace_ReturnsExcess(Organization organization)
    {
        // Arrange — 220 accounts, base 50, grace 150 => bill for 20.
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            ServiceAccountGrace = 150,
            SmServiceAccounts = 220
        };

        // Act / Assert
        Assert.Equal(20, sut.SmServiceAccountsExcludingBase);
    }

    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_AboveBaseline_WithNoGrace_ReturnsExcess(Organization organization)
    {
        // Arrange — ServiceAccountGrace defaults to 0, preserving the original (clamped) behavior.
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmServiceAccounts = 70
        };

        // Act / Assert
        Assert.Equal(20, sut.SmServiceAccountsExcludingBase);
    }

    // PM-37511: migrated Teams Monthly org: base 20, grace 30 => free ceiling of 50. 60 accounts bill for 10.
    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_TeamsMonthly_AboveBaselinePlusGrace_ReturnsExcess(Organization organization)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.TeamsMonthly); // BaseServiceAccount = 20
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            ServiceAccountGrace = 30,
            SmServiceAccounts = 60
        };

        // Act / Assert
        Assert.Equal(10, sut.SmServiceAccountsExcludingBase);
    }

    // PM-37511: 80 accounts, base 20, grace 30 => bill for 30 (covers the above-baseline billed quantity).
    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_TeamsMonthly_FarAboveBaselinePlusGrace_ReturnsExcess(Organization organization)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.TeamsMonthly); // BaseServiceAccount = 20
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            ServiceAccountGrace = 30,
            SmServiceAccounts = 80
        };

        // Act / Assert
        Assert.Equal(30, sut.SmServiceAccountsExcludingBase);
    }

    // PM-37511: Teams Annually keeps the 2020 SM baseline of 50 (no reduction => no grace). 70 accounts
    // bill for 20. Proves the annual cadence is unchanged; relies on the TeamsPlan mock fix (base 50).
    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_TeamsAnnual_NoGrace_ReturnsExcess(Organization organization)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.TeamsAnnually); // BaseServiceAccount = 50
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            SmServiceAccounts = 70
        };

        // Act / Assert
        Assert.Equal(20, sut.SmServiceAccountsExcludingBase);
    }

    // PM-37511: within the free ceiling (base 20 + grace 30 = 50), a migrated Teams Monthly org bills 0 —
    // additions that still fit inside grace produce no billable excess.
    [Theory]
    [BitAutoData]
    public void SmServiceAccountsExcludingBase_TeamsMonthly_WithinBaselinePlusGrace_ReturnsZero(Organization organization)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.TeamsMonthly); // BaseServiceAccount = 20
        organization.PlanType = plan.Type;

        var sut = new SecretsManagerSubscriptionUpdate(organization, plan, false)
        {
            ServiceAccountGrace = 30,
            SmServiceAccounts = 45
        };

        // Act / Assert
        Assert.Equal(0, sut.SmServiceAccountsExcludingBase);
    }
}
