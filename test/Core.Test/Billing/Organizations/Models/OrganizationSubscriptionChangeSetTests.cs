using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Test.Billing.Mocks;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Models;

public class OrganizationSubscriptionChangeTests
{
    [Fact]
    public void ImplicitConversion_AddItem_SetsCorrectFlags()
    {
        OrganizationSubscriptionChange change = new AddItem("price_123", 5);

        Assert.True(change.IsItemAddition);
        Assert.False(change.IsItemPriceChange);
        Assert.False(change.IsItemRemoval);
        Assert.False(change.IsItemQuantityUpdate);
        Assert.True(change.IsStructural);
    }

    [Fact]
    public void ImplicitConversion_ChangeItemPrice_SetsCorrectFlags()
    {
        OrganizationSubscriptionChange change = new ChangeItemPrice("price_old", "price_new", null);

        Assert.False(change.IsItemAddition);
        Assert.True(change.IsItemPriceChange);
        Assert.False(change.IsItemRemoval);
        Assert.False(change.IsItemQuantityUpdate);
        Assert.True(change.IsStructural);
    }

    [Fact]
    public void ImplicitConversion_RemoveItem_SetsCorrectFlags()
    {
        OrganizationSubscriptionChange change = new RemoveItem("price_123");

        Assert.False(change.IsItemAddition);
        Assert.False(change.IsItemPriceChange);
        Assert.True(change.IsItemRemoval);
        Assert.False(change.IsItemQuantityUpdate);
        Assert.True(change.IsStructural);
    }

    [Fact]
    public void ImplicitConversion_UpdateItemQuantity_SetsCorrectFlags()
    {
        OrganizationSubscriptionChange change = new UpdateItemQuantity("price_123", 10);

        Assert.False(change.IsItemAddition);
        Assert.False(change.IsItemPriceChange);
        Assert.False(change.IsItemRemoval);
        Assert.True(change.IsItemQuantityUpdate);
        Assert.False(change.IsStructural);
    }

    [Fact]
    public void ImplicitConversion_UpdateItemQuantityToZero_IsStructural()
    {
        OrganizationSubscriptionChange change = new UpdateItemQuantity("price_123", 0);

        Assert.True(change.IsItemQuantityUpdate);
        Assert.True(change.IsStructural);
    }
}

public class OrganizationSubscriptionChangeSetTests
{
    [Fact]
    public void UpdatePasswordManagerSeats_CreatesCorrectChangeSet()
    {
        var plan = MockPlans.Get(PlanType.TeamsAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.UpdatePasswordManagerSeats(plan, 25);

        var change = Assert.Single(changeSet.Changes);
        Assert.True(change.IsItemQuantityUpdate);
        Assert.False(change.IsStructural);

        var update = change.AsT3;
        Assert.Equal(plan.PasswordManager.StripeSeatPlanId, update.PriceId);
        Assert.Equal(25, update.Quantity);
    }

    [Fact]
    public void UpdateStorage_CreatesCorrectChangeSet()
    {
        var plan = MockPlans.Get(PlanType.TeamsAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.UpdateStorage(plan, 3);

        var change = Assert.Single(changeSet.Changes);
        Assert.True(change.IsItemQuantityUpdate);

        var update = change.AsT3;
        Assert.Equal(plan.PasswordManager.StripeStoragePlanId, update.PriceId);
        Assert.Equal(3, update.Quantity);
    }

    [Fact]
    public void UpdateSecretsManagerSeats_CreatesCorrectChangeSet()
    {
        var plan = MockPlans.Get(PlanType.TeamsAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.UpdateSecretsManagerSeats(plan, 10);

        var change = Assert.Single(changeSet.Changes);
        Assert.True(change.IsItemQuantityUpdate);

        var update = change.AsT3;
        Assert.Equal(plan.SecretsManager.StripeSeatPlanId, update.PriceId);
        Assert.Equal(10, update.Quantity);
    }

    [Fact]
    public void UpdateSecretsManagerServiceAccounts_CreatesCorrectChangeSet()
    {
        var plan = MockPlans.Get(PlanType.TeamsAnnually);

        var changeSet = OrganizationSubscriptionChangeSet.UpdateSecretsManagerServiceAccounts(plan, 50);

        var change = Assert.Single(changeSet.Changes);
        Assert.True(change.IsItemQuantityUpdate);

        var update = change.AsT3;
        Assert.Equal(plan.SecretsManager.StripeServiceAccountPlanId, update.PriceId);
        Assert.Equal(50, update.Quantity);
    }
}

public class OrganizationSubscriptionChangeSetBuilderTests
{
    [Fact]
    public void AddItem_AddsToChanges()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .AddItem("price_add", 3)
            .Build();

        var change = Assert.Single(changeSet.Changes);
        Assert.True(change.IsItemAddition);

        var item = change.AsT0;
        Assert.Equal("price_add", item.PriceId);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void ChangeItemPrice_AddsToChanges()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .ChangeItemPrice("price_old", "price_new")
            .Build();

        var change = Assert.Single(changeSet.Changes);
        Assert.True(change.IsItemPriceChange);

        var item = change.AsT1;
        Assert.Equal("price_old", item.CurrentPriceId);
        Assert.Equal("price_new", item.UpdatedPriceId);
        Assert.Null(item.Quantity);
    }

    [Fact]
    public void ChangeItemPrice_WithQuantity_AddsToChanges()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .ChangeItemPrice("price_old", "price_new", 7)
            .Build();

        var change = Assert.Single(changeSet.Changes);
        var item = change.AsT1;
        Assert.Equal(7, item.Quantity);
    }

    [Fact]
    public void RemoveItem_AddsToChanges()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .RemoveItem("price_remove")
            .Build();

        var change = Assert.Single(changeSet.Changes);
        Assert.True(change.IsItemRemoval);

        var item = change.AsT2;
        Assert.Equal("price_remove", item.PriceId);
    }

    [Fact]
    public void UpdateItemQuantity_AddsToChanges()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .UpdateItemQuantity("price_qty", 15)
            .Build();

        var change = Assert.Single(changeSet.Changes);
        Assert.True(change.IsItemQuantityUpdate);

        var item = change.AsT3;
        Assert.Equal("price_qty", item.PriceId);
        Assert.Equal(15, item.Quantity);
    }

    [Fact]
    public void Build_WithMultipleChanges_PreservesOrder()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .AddItem("price_1", 1)
            .RemoveItem("price_2")
            .ChangeItemPrice("price_3", "price_4")
            .UpdateItemQuantity("price_5", 10)
            .Build();

        Assert.Equal(4, changeSet.Changes.Count);
        Assert.True(changeSet.Changes[0].IsItemAddition);
        Assert.True(changeSet.Changes[1].IsItemRemoval);
        Assert.True(changeSet.Changes[2].IsItemPriceChange);
        Assert.True(changeSet.Changes[3].IsItemQuantityUpdate);
    }

    [Fact]
    public void Build_WithNoChanges_ReturnsEmptyChangeSet()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .Build();

        Assert.Empty(changeSet.Changes);
    }

    [Fact]
    public void Build_ReturnsReadOnlyChanges()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .AddItem("price_1", 1)
            .Build();

        Assert.IsAssignableFrom<IReadOnlyList<OrganizationSubscriptionChange>>(changeSet.Changes);
    }

    [Fact]
    public void Build_MixedStructuralAndNonStructural()
    {
        var changeSet = new OrganizationSubscriptionChangeSetBuilder()
            .AddItem("price_add", 1)
            .UpdateItemQuantity("price_qty", 5)
            .Build();

        Assert.Equal(2, changeSet.Changes.Count);
        Assert.True(changeSet.Changes[0].IsStructural);
        Assert.False(changeSet.Changes[1].IsStructural);
    }
}
