using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.AnnualUpgradeOffer;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Test.Billing.Mocks.Plans;
using Stripe;
using Xunit;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer;

using static StripeConstants;

public class AnnualUpgradeSchedulePhaseRebuilderTests
{
    private static readonly Plan _source = new TeamsPlan(false);
    private static readonly Plan _target = new TeamsPlan(true);

    private static string MonthlySeat => new TeamsPlan(false).PasswordManager.StripeSeatPlanId;
    private static string AnnualSeat => new TeamsPlan(true).PasswordManager.StripeSeatPlanId;

    private static SubscriptionSchedulePhase Phase(
        string priceId,
        long quantity,
        List<SubscriptionSchedulePhaseDiscount>? discounts = null,
        List<SubscriptionSchedulePhaseItemDiscount>? itemDiscounts = null) =>
        new()
        {
            StartDate = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
            Items = [new SubscriptionSchedulePhaseItem { PriceId = priceId, Quantity = quantity, Discounts = itemDiscounts }],
            Discounts = discounts,
            ProrationBehavior = ProrationBehavior.None
        };

    [Fact]
    public void BuildUpdatedPhases_PhaseLevelDiscountWithDiscountId_IsCarriedByReuse()
    {
        var phases = new List<SubscriptionSchedulePhase>
        {
            Phase(MonthlySeat, 5, discounts: [new SubscriptionSchedulePhaseDiscount { DiscountId = "di_own", CouponId = "coupon_own" }]),
            Phase(AnnualSeat, 5, discounts: [new SubscriptionSchedulePhaseDiscount { DiscountId = "di_own", CouponId = "coupon_own" }])
        };

        var result = AnnualUpgradeSchedulePhaseRebuilder.BuildUpdatedPhases(phases, [], _source, _target);

        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Discounts);
        Assert.Equal("di_own", result[0].Discounts[0].Discount);
        Assert.Null(result[0].Discounts[0].Coupon);
        Assert.Equal("di_own", result[1].Discounts[0].Discount);
        Assert.Null(result[1].Discounts[0].Coupon);
    }

    [Fact]
    public void BuildUpdatedPhases_PhaseLevelDiscountWithoutDiscountId_FallsBackToCoupon()
    {
        var phases = new List<SubscriptionSchedulePhase>
        {
            Phase(MonthlySeat, 5, discounts: [new SubscriptionSchedulePhaseDiscount { CouponId = "coupon_only" }])
        };

        var result = AnnualUpgradeSchedulePhaseRebuilder.BuildUpdatedPhases(phases, [], _source, _target);

        Assert.Equal("coupon_only", result[0].Discounts[0].Coupon);
        Assert.Null(result[0].Discounts[0].Discount);
    }

    [Fact]
    public void BuildUpdatedPhases_NoPhaseLevelDiscounts_ProducesNull()
    {
        var phases = new List<SubscriptionSchedulePhase> { Phase(MonthlySeat, 5) };

        var result = AnnualUpgradeSchedulePhaseRebuilder.BuildUpdatedPhases(phases, [], _source, _target);

        Assert.Null(result[0].Discounts);
    }

    [Fact]
    public void BuildUpdatedPhases_ItemDiscounts_AreCarriedByCoupon()
    {
        var phases = new List<SubscriptionSchedulePhase>
        {
            Phase(MonthlySeat, 5, itemDiscounts: [new SubscriptionSchedulePhaseItemDiscount { DiscountId = "di_item", CouponId = "coupon_item" }])
        };

        var result = AnnualUpgradeSchedulePhaseRebuilder.BuildUpdatedPhases(phases, [], _source, _target);

        Assert.Equal("coupon_item", result[0].Items[0].Discounts[0].Coupon);
        Assert.Null(result[0].Items[0].Discounts[0].Discount);
    }

    [Fact]
    public void BuildUpdatedPhases_PreservesTimingMetadataAndProration()
    {
        var phase = Phase(MonthlySeat, 5);
        phase.Metadata = new Dictionary<string, string> { ["annualUpgrade"] = "TeamsMonthly" };

        var result = AnnualUpgradeSchedulePhaseRebuilder.BuildUpdatedPhases([phase], [], _source, _target);

        Assert.True(result[0].StartDate == phase.StartDate);
        Assert.True(result[0].EndDate == phase.EndDate);
        Assert.Equal(phase.Metadata, result[0].Metadata);
        Assert.Equal(ProrationBehavior.None, result[0].ProrationBehavior);
    }

    [Fact]
    public void BuildUpdatedPhases_AppliesQuantityUpdateToTheMatchingItem()
    {
        var phases = new List<SubscriptionSchedulePhase>
        {
            Phase(MonthlySeat, 5),
            Phase(AnnualSeat, 5)
        };
        IReadOnlyList<OrganizationSubscriptionChange> changes = [new UpdateItemQuantity(MonthlySeat, 10)];

        var result = AnnualUpgradeSchedulePhaseRebuilder.BuildUpdatedPhases(phases, changes, _source, _target);

        // Phase 1 stays on source prices, so the monthly seat quantity updates in place.
        Assert.Equal(10, result[0].Items.Single(i => i.Price == MonthlySeat).Quantity);
        // Phase 2 translates the change to the annual seat price.
        Assert.Equal(10, result[1].Items.Single(i => i.Price == AnnualSeat).Quantity);
    }
}
