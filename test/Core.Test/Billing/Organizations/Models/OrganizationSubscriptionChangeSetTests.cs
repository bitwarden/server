using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Models.StaticStore;
using Bit.Core.Test.Billing.Mocks;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Models;

public class OrganizationSubscriptionChangeSetBuilderTests
{
    private static Plan GetPlan(PlanType planType = PlanType.TeamsAnnually) =>
        MockPlans.Get(planType);

    [Fact]
    public void AddStorage_DoesNotSetChargeImmediately()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .AddStorage(3)
            .Build();

        Assert.False(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var item = change.AsT0;
        Assert.Equal(plan.PasswordManager.StripeStoragePlanId, item.PriceId);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void UpdateStorage_DoesNotSetChargeImmediately()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .UpdateStorage(5)
            .Build();

        Assert.False(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var item = change.AsT3;
        Assert.Equal(plan.PasswordManager.StripeStoragePlanId, item.PriceId);
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void AddServiceAccounts_DoesNotSetChargeImmediately()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .AddServiceAccounts(10)
            .Build();

        Assert.False(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var item = change.AsT0;
        Assert.Equal(plan.SecretsManager.StripeServiceAccountPlanId, item.PriceId);
        Assert.Equal(10, item.Quantity);
    }

    [Fact]
    public void UpdateServiceAccounts_DoesNotSetChargeImmediately()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .UpdateServiceAccounts(20)
            .Build();

        Assert.False(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var item = change.AsT3;
        Assert.Equal(plan.SecretsManager.StripeServiceAccountPlanId, item.PriceId);
        Assert.Equal(20, item.Quantity);
    }

    [Fact]
    public void UpdatePasswordManagerSeats_DoesNotSetChargeImmediately()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .UpdatePasswordManagerSeats(25)
            .Build();

        Assert.False(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var item = change.AsT3;
        Assert.Equal(plan.PasswordManager.StripeSeatPlanId, item.PriceId);
        Assert.Equal(25, item.Quantity);
    }

    [Fact]
    public void UpdateSecretsManagerSeats_DoesNotSetChargeImmediately()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .UpdateSecretsManagerSeats(10)
            .Build();

        Assert.False(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var item = change.AsT3;
        Assert.Equal(plan.SecretsManager.StripeSeatPlanId, item.PriceId);
        Assert.Equal(10, item.Quantity);
    }

    [Fact]
    public void EstablishSponsorship_SetsChargeImmediately()
    {
        var plan = GetPlan(PlanType.FamiliesAnnually);
        var sponsoredPlan = new SponsoredPlan { StripePlanId = "sponsored_plan_id" };

        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .EstablishSponsorship(sponsoredPlan)
            .Build();

        Assert.True(changeSet.ChargeImmediately);
        Assert.Equal(2, changeSet.Changes.Count);

        var removeItem = changeSet.Changes[0].AsT2;
        Assert.Equal(plan.PasswordManager.StripePlanId, removeItem.PriceId);

        var addItem = changeSet.Changes[1].AsT0;
        Assert.Equal("sponsored_plan_id", addItem.PriceId);
        Assert.Equal(1, addItem.Quantity);
    }

    [Fact]
    public void ChangePasswordManagerPrice_SetsChargeImmediately()
    {
        var currentPlan = GetPlan(PlanType.TeamsAnnually);
        var targetPlan = GetPlan(PlanType.EnterpriseAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.Builder(currentPlan)
            .ChangePasswordManagerPrice(targetPlan)
            .Build();

        Assert.True(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var priceChange = change.AsT1;
        Assert.Equal(currentPlan.PasswordManager.StripeSeatPlanId, priceChange.CurrentPriceId);
        Assert.Equal(targetPlan.PasswordManager.StripeSeatPlanId, priceChange.UpdatedPriceId);
        Assert.Null(priceChange.Quantity);
    }

    [Fact]
    public void ChangePasswordManagerPrice_NonSeatBased_UsesStripePlanId_AndSetsBaseSeatsQuantity()
    {
        var currentPlan = GetPlan(PlanType.FamiliesAnnually);
        var targetPlan = GetPlan(PlanType.EnterpriseAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.Builder(currentPlan)
            .ChangePasswordManagerPrice(targetPlan)
            .Build();

        Assert.True(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var priceChange = change.AsT1;
        // Families is non-seat-based, so should use StripePlanId
        Assert.Equal(currentPlan.PasswordManager.StripePlanId, priceChange.CurrentPriceId);
        // Enterprise is seat-based, so should use StripeSeatPlanId
        Assert.Equal(targetPlan.PasswordManager.StripeSeatPlanId, priceChange.UpdatedPriceId);
        // Quantity should carry over Families BaseSeats (6) to the seat-based plan
        Assert.Equal(currentPlan.PasswordManager.BaseSeats, priceChange.Quantity);
    }

    [Fact]
    public void ChangePasswordManagerPrice_SeatBased_UsesStripeSeatPlanId()
    {
        var currentPlan = GetPlan(PlanType.TeamsAnnually);
        var targetPlan = GetPlan(PlanType.EnterpriseAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.Builder(currentPlan)
            .ChangePasswordManagerPrice(targetPlan)
            .Build();

        var change = Assert.Single(changeSet.Changes);
        var priceChange = change.AsT1;
        Assert.Equal(currentPlan.PasswordManager.StripeSeatPlanId, priceChange.CurrentPriceId);
        Assert.Equal(targetPlan.PasswordManager.StripeSeatPlanId, priceChange.UpdatedPriceId);
    }

    [Fact]
    public void ChangeStoragePrice_SetsChargeImmediately()
    {
        var currentPlan = GetPlan(PlanType.TeamsAnnually);
        var targetPlan = GetPlan(PlanType.EnterpriseAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.Builder(currentPlan)
            .ChangeStoragePrice(targetPlan)
            .Build();

        Assert.True(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var priceChange = change.AsT1;
        Assert.Equal(currentPlan.PasswordManager.StripeStoragePlanId, priceChange.CurrentPriceId);
        Assert.Equal(targetPlan.PasswordManager.StripeStoragePlanId, priceChange.UpdatedPriceId);
    }

    [Fact]
    public void ChangeSecretsManagerSeatPrice_SetsChargeImmediately()
    {
        var currentPlan = GetPlan(PlanType.TeamsAnnually);
        var targetPlan = GetPlan(PlanType.EnterpriseAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.Builder(currentPlan)
            .ChangeSecretsManagerSeatPrice(targetPlan)
            .Build();

        Assert.True(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var priceChange = change.AsT1;
        Assert.Equal(currentPlan.SecretsManager.StripeSeatPlanId, priceChange.CurrentPriceId);
        Assert.Equal(targetPlan.SecretsManager.StripeSeatPlanId, priceChange.UpdatedPriceId);
    }

    [Fact]
    public void ChangeServiceAccountPrice_SetsChargeImmediately()
    {
        var currentPlan = GetPlan(PlanType.TeamsAnnually);
        var targetPlan = GetPlan(PlanType.EnterpriseAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.Builder(currentPlan)
            .ChangeServiceAccountPrice(targetPlan)
            .Build();

        Assert.True(changeSet.ChargeImmediately);

        var change = Assert.Single(changeSet.Changes);
        var priceChange = change.AsT1;
        Assert.Equal(currentPlan.SecretsManager.StripeServiceAccountPlanId, priceChange.CurrentPriceId);
        Assert.Equal(targetPlan.SecretsManager.StripeServiceAccountPlanId, priceChange.UpdatedPriceId);
    }

    [Fact]
    public void Build_MixedSeatAndAddOn_DoesNotSetChargeImmediately()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .UpdatePasswordManagerSeats(10)
            .AddStorage(3)
            .Build();

        Assert.False(changeSet.ChargeImmediately);
        Assert.Equal(2, changeSet.Changes.Count);
    }

    [Fact]
    public void Build_MixedStructuralAndAddOn_SetsChargeImmediately()
    {
        var currentPlan = GetPlan(PlanType.TeamsAnnually);
        var targetPlan = GetPlan(PlanType.EnterpriseAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.Builder(currentPlan)
            .UpdateStorage(5)
            .ChangePasswordManagerPrice(targetPlan)
            .Build();

        Assert.True(changeSet.ChargeImmediately);
        Assert.Equal(2, changeSet.Changes.Count);
    }

    [Fact]
    public void Build_WithNoChanges_ReturnsEmptyChangeSet()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan).Build();

        Assert.Empty(changeSet.Changes);
        Assert.False(changeSet.ChargeImmediately);
    }

    [Fact]
    public void Build_ReturnsReadOnlyChanges()
    {
        var plan = GetPlan();
        var changeSet = OrganizationSubscriptionChangeSet.Builder(plan)
            .AddStorage(1)
            .Build();

        Assert.IsAssignableFrom<IReadOnlyList<OrganizationSubscriptionChange>>(changeSet.Changes);
    }
}
