using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration;
using Bit.Core.Test.Billing.Mocks;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration;

public class OrganizationPlanMigrationPriceMapperTests
{
    [Fact]
    public void MapOrNull_PmSeat_ReturnsTargetPmSeat()
    {
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        var result = OrganizationPlanMigrationPriceMapper.MapOrNull(
            source.PasswordManager.StripeSeatPlanId, source, target);

        Assert.Equal(target.PasswordManager.StripeSeatPlanId, result);
    }

    [Fact]
    public void MapOrNull_SmServiceAccount_ReturnsTargetSmServiceAccount()
    {
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        var result = OrganizationPlanMigrationPriceMapper.MapOrNull(
            source.SecretsManager.StripeServiceAccountPlanId, source, target);

        Assert.Equal(target.SecretsManager.StripeServiceAccountPlanId, result);
    }

    // PM-37511: the Teams Annual SM add-on maps source -> target StripeServiceAccountPlanId via the same
    // mapper, so the line item swaps to the target price with quantity unchanged and no grace. The 2020
    // and current annual SA price ids differ, so this asserts a real swap, not a pass-through.
    [Fact]
    public void MapOrNull_TeamsAnnualSmServiceAccount_ReturnsTargetSmServiceAccount()
    {
        var source = MockPlans.Get(PlanType.TeamsAnnually2020);
        var target = MockPlans.Get(PlanType.TeamsAnnually);

        var result = OrganizationPlanMigrationPriceMapper.MapOrNull(
            source.SecretsManager.StripeServiceAccountPlanId, source, target);

        Assert.Equal(target.SecretsManager.StripeServiceAccountPlanId, result);
        Assert.NotEqual(source.SecretsManager.StripeServiceAccountPlanId, result);
    }

    [Fact]
    public void MapOrNull_UnknownPriceId_ReturnsNull()
    {
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        var result = OrganizationPlanMigrationPriceMapper.MapOrNull("unmapped-price", source, target);

        Assert.Null(result);
    }

    [Fact]
    public void MapOrNull_SmSeatWhenSourceSmIsNull_ReturnsNull()
    {
        var source = MockPlans.Get(PlanType.FamiliesAnnually);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        var result = OrganizationPlanMigrationPriceMapper.MapOrNull(
            target.SecretsManager.StripeSeatPlanId, source, target);

        Assert.Null(result);
    }

    [Fact]
    public void MapOrNull_SmSeatWhenTargetSmIsNull_ReturnsNull()
    {
        var source = MockPlans.Get(PlanType.EnterpriseAnnually);
        var target = MockPlans.Get(PlanType.FamiliesAnnually);

        var result = OrganizationPlanMigrationPriceMapper.MapOrNull(
            source.SecretsManager.StripeSeatPlanId, source, target);

        Assert.Null(result);
    }

    [Fact]
    public void MapOrPassThrough_SamePlanInstance_ReturnsInputUnchanged()
    {
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually2020);

        var resultForKnownSlot = OrganizationPlanMigrationPriceMapper.MapOrPassThrough(
            plan.PasswordManager.StripeSeatPlanId, plan, plan);
        var resultForUnknown = OrganizationPlanMigrationPriceMapper.MapOrPassThrough(
            "unmapped", plan, plan);

        Assert.Equal(plan.PasswordManager.StripeSeatPlanId, resultForKnownSlot);
        Assert.Equal("unmapped", resultForUnknown);
    }

    [Fact]
    public void MapOrPassThrough_UnknownPriceId_ReturnsInput()
    {
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        var result = OrganizationPlanMigrationPriceMapper.MapOrPassThrough(
            "unmapped-price", source, target);

        Assert.Equal("unmapped-price", result);
    }

    [Fact]
    public void MapOrPassThrough_MappedPriceId_ReturnsTargetSlotValue()
    {
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        var result = OrganizationPlanMigrationPriceMapper.MapOrPassThrough(
            source.PasswordManager.StripeSeatPlanId, source, target);

        Assert.Equal(target.PasswordManager.StripeSeatPlanId, result);
    }
}
