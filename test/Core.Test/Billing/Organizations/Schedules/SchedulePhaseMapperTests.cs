using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Test.Billing.Mocks.Plans;
using Stripe;
using Xunit;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Test.Billing.Organizations.Schedules;

using static StripeConstants;

public class SchedulePhaseMapperTests
{
    private static readonly Plan _source = new TeamsPlan(false);
    private static readonly Plan _target = new TeamsPlan(true);

    private static string MonthlySeat => new TeamsPlan(false).PasswordManager.StripeSeatPlanId;
    private static string AnnualSeat => new TeamsPlan(true).PasswordManager.StripeSeatPlanId;

    private static SubscriptionSchedulePhase Phase(
        string priceId,
        long quantity,
        List<SubscriptionSchedulePhaseItemDiscount>? itemDiscounts = null) =>
        new()
        {
            StartDate = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
            Items = [new SubscriptionSchedulePhaseItem { PriceId = priceId, Quantity = quantity, Discounts = itemDiscounts }],
            ProrationBehavior = ProrationBehavior.None
        };

    // These behaviors are about item projection and matching mechanics, not price translation, so
    // source and target are the same plan: Translate becomes a no-op and price ids stay literal.
    // Price translation itself is covered separately below.

    [Fact]
    public void ApplyChangesToPhaseItems_ExistingItem_ProjectsPriceAndQuantity()
    {
        var phase = Phase(MonthlySeat, 5);

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, [], _source, _source);

        var item = Assert.Single(result);
        Assert.Equal(MonthlySeat, item.Price);
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_ExistingItemDiscounts_CopiedByCoupon()
    {
        var phase = Phase(MonthlySeat, 5,
            itemDiscounts: [new SubscriptionSchedulePhaseItemDiscount { DiscountId = "di_item", CouponId = "coupon_item" }]);

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, [], _source, _source);

        var discount = Assert.Single(Assert.Single(result).Discounts);
        Assert.Equal("coupon_item", discount.Coupon);
        Assert.Null(discount.Discount);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_ExistingItemWithNoDiscounts_YieldsNullDiscounts()
    {
        var phase = Phase(MonthlySeat, 5);

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, [], _source, _source);

        Assert.Null(Assert.Single(result).Discounts);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_AddItem_AddsTranslatedItem()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new AddItem(MonthlySeat, 3)];

        // Source and target differ here on purpose, so the added item is translated to the
        // target-plan seat price rather than staying on the price the caller passed in.
        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _target);

        Assert.Contains(result, i => i.Price == AnnualSeat && i.Quantity == 3);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_ChangeItemPrice_MatchingItem_TranslatesPriceAndAppliesQuantity()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes =
            [new ChangeItemPrice(MonthlySeat, "price_upgraded", 10)];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _source);

        var item = Assert.Single(result);
        Assert.Equal("price_upgraded", item.Price);
        Assert.Equal(10, item.Quantity);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_ChangeItemPrice_NonMatchingItem_IsNoOp()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes =
            [new ChangeItemPrice("price_other", "price_other_updated", 10)];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _source);

        var item = Assert.Single(result);
        Assert.Equal(MonthlySeat, item.Price);
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_RemoveItem_RemovesMatchingTranslatedItem()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new RemoveItem(MonthlySeat)];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _source);

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_RemoveItem_NonMatchingItem_IsNoOp()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new RemoveItem("price_other")];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _source);

        Assert.Single(result);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_UpdateItemQuantity_ExistingItem_UpdatesQuantity()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new UpdateItemQuantity(MonthlySeat, 10)];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _source);

        var item = Assert.Single(result);
        Assert.Equal(MonthlySeat, item.Price);
        Assert.Equal(10, item.Quantity);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_UpdateItemQuantity_Zero_RemovesItem()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new UpdateItemQuantity(MonthlySeat, 0)];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _source);

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_UpdateItemQuantity_NonMatchingPrice_AddsItem()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new UpdateItemQuantity("price_other", 7)];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _source);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Price == "price_other" && i.Quantity == 7);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_TargetPhase_TranslatesSeatPriceToAnnual()
    {
        var phase = Phase(AnnualSeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new UpdateItemQuantity(MonthlySeat, 10)];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _target);

        Assert.Equal(10, result.Single(i => i.Price == AnnualSeat).Quantity);
    }

    [Fact]
    public void ApplyChangesToPhaseItems_SourcePhase_StaysMonthly()
    {
        var phase = Phase(MonthlySeat, 5);
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new UpdateItemQuantity(MonthlySeat, 10)];

        var result = SchedulePhaseMapper.ApplyChangesToPhaseItems(phase.Items, changes, _source, _source);

        Assert.Equal(10, result.Single(i => i.Price == MonthlySeat).Quantity);
    }

    [Fact]
    public void PhaseUsesTargetPlanPrices_ItemsUseTargetPrices_ReturnsTrue()
    {
        var phase = Phase(AnnualSeat, 5);

        Assert.True(SchedulePhaseMapper.PhaseUsesTargetPlanPrices(phase, _target));
    }

    [Fact]
    public void PhaseUsesTargetPlanPrices_ItemsUseSourcePrices_ReturnsFalse()
    {
        var phase = Phase(MonthlySeat, 5);

        Assert.False(SchedulePhaseMapper.PhaseUsesTargetPlanPrices(phase, _target));
    }
}
