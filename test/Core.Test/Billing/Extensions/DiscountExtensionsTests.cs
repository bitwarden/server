using Bit.Core.Billing.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Extensions;

public class DiscountExtensionsTests
{
    // Live discount: builders read the coupon id off Source.CouponId (populated when
    // "discounts.source" is expanded), and emit the discount by its own Id at phase/sub scope.
    private static Discount LiveDiscount(string discountId, string couponId) =>
        new() { Id = discountId, Source = new DiscountSource { CouponId = couponId } };

    private static Subscription Sub(Discount? customer = null, params Discount[] discounts) =>
        new()
        {
            Customer = new Customer { Discount = customer },
            Discounts = discounts.Length == 0 ? null : [.. discounts]
        };

    // ---- BuildPhaseLevelDiscounts ----

    [Fact]
    public void BuildPhaseLevelDiscounts_LiveSubscriptionDiscount_CarriedByDiscountId()
    {
        var result = DiscountExtensions.BuildPhaseLevelDiscounts(
            Sub(discounts: LiveDiscount("di_1", "cpn_1")), []);

        Assert.Single(result!);
        Assert.Equal("di_1", result![0].Discount);
        Assert.Null(result[0].Coupon);
    }

    [Fact]
    public void BuildPhaseLevelDiscounts_CustomerCoupon_CarriedByCouponId_First()
    {
        var result = DiscountExtensions.BuildPhaseLevelDiscounts(
            Sub(customer: LiveDiscount("di_c", "cpn_customer"), discounts: LiveDiscount("di_1", "cpn_1")), []);

        Assert.Equal(2, result!.Count);
        Assert.Equal("cpn_customer", result[0].Coupon);
        Assert.Equal("di_1", result[1].Discount);
    }

    [Fact]
    public void BuildPhaseLevelDiscounts_PreservedFutureCoupons_CarriedByCouponId()
    {
        var result = DiscountExtensions.BuildPhaseLevelDiscounts(
            Sub(), [], preservedCouponIds: ["cpn_future"]);

        Assert.Single(result!);
        Assert.Equal("cpn_future", result![0].Coupon);
    }

    [Fact]
    public void BuildPhaseLevelDiscounts_NewCoupons_CarriedByCouponId()
    {
        var result = DiscountExtensions.BuildPhaseLevelDiscounts(Sub(), ["cpn_new"]);

        Assert.Single(result!);
        Assert.Equal("cpn_new", result![0].Coupon);
    }

    [Fact]
    public void BuildPhaseLevelDiscounts_CouponAppearsOnCustomerAndSubscription_DeDuped()
    {
        var result = DiscountExtensions.BuildPhaseLevelDiscounts(
            Sub(customer: LiveDiscount("di_c", "shared"), discounts: LiveDiscount("di_1", "shared")), []);

        Assert.Single(result!);
        Assert.Equal("shared", result![0].Coupon);
    }

    [Fact]
    public void BuildPhaseLevelDiscounts_Empty_ReturnsNull()
    {
        Assert.Null(DiscountExtensions.BuildPhaseLevelDiscounts(Sub(), []));
    }

    // ---- BuildCurrentPhaseDiscounts ----

    [Fact]
    public void BuildCurrentPhaseDiscounts_LiveDiscounts_CarriedByDiscountId()
    {
        var result = DiscountExtensions.BuildCurrentPhaseDiscounts(
            Sub(discounts: LiveDiscount("di_1", "cpn_1")));

        Assert.Single(result!);
        Assert.Equal("di_1", result![0].Discount);
        Assert.Null(result[0].Coupon);
    }

    [Fact]
    public void BuildCurrentPhaseDiscounts_CustomerCoupon_NotIncluded()
    {
        var result = DiscountExtensions.BuildCurrentPhaseDiscounts(
            Sub(customer: LiveDiscount("di_c", "cpn_customer"), discounts: LiveDiscount("di_1", "cpn_1")));

        Assert.Single(result!);
        Assert.Equal("di_1", result![0].Discount);
    }

    [Fact]
    public void BuildCurrentPhaseDiscounts_Empty_ReturnsNull()
    {
        Assert.Null(DiscountExtensions.BuildCurrentPhaseDiscounts(
            Sub(customer: LiveDiscount("di_c", "cpn_customer"))));
    }

    // ---- BuildPhaseItemLevelDiscounts ----

    [Fact]
    public void BuildPhaseItemLevelDiscounts_CouponIds_CarriedByCouponId_DeDuped()
    {
        var result = DiscountExtensions.BuildPhaseItemLevelDiscounts(["cpn_a", "cpn_a", "cpn_b"]);

        Assert.Equal(2, result!.Count);
        Assert.Equal("cpn_a", result[0].Coupon);
        Assert.Equal("cpn_b", result[1].Coupon);
    }

    [Fact]
    public void BuildPhaseItemLevelDiscounts_NullAndEmptySkipped_ReturnsNullWhenAllEmpty()
    {
        Assert.Null(DiscountExtensions.BuildPhaseItemLevelDiscounts([null, "", null]));
    }

    // ---- BuildSubscriptionLevelDiscounts ----

    [Fact]
    public void BuildSubscriptionLevelDiscounts_LiveDiscount_ByDiscountId_NewByCoupon()
    {
        var result = DiscountExtensions.BuildSubscriptionLevelDiscounts(
            Sub(discounts: LiveDiscount("di_1", "cpn_1")), ["cpn_new"]);

        Assert.Equal(2, result!.Count);
        Assert.Equal("di_1", result[0].Discount);
        Assert.Equal("cpn_new", result[1].Coupon);
    }

    [Fact]
    public void BuildSubscriptionLevelDiscounts_Empty_ReturnsNull()
    {
        Assert.Null(DiscountExtensions.BuildSubscriptionLevelDiscounts(Sub(), []));
    }

    // ---- RequireScheduleDiscountExpansions ----

    [Fact]
    public void RequireScheduleDiscountExpansions_UnexpandedDiscounts_Throws()
    {
        // Stripe.net's Subscription.Discounts setter throws on a null element (it assumes fully
        // expanded input), so an unexpanded discount ID can only be reproduced the way Stripe.net
        // itself produces one: deserializing a subscription whose "discounts" array holds bare ID
        // strings instead of expanded objects.
        var sub = JsonConvert.DeserializeObject<Subscription>("""{"id":"sub_1","discounts":["di_1"]}""");

        Assert.Throws<InvalidOperationException>(() =>
            DiscountExtensions.RequireScheduleDiscountExpansions(sub!, NullLogger.Instance));
    }

    [Fact]
    public void RequireScheduleDiscountExpansions_MissingCustomer_Throws()
    {
        var sub = new Subscription { Id = "sub_1", Customer = null };

        Assert.Throws<InvalidOperationException>(() =>
            DiscountExtensions.RequireScheduleDiscountExpansions(sub, NullLogger.Instance));
    }

    [Fact]
    public void RequireScheduleDiscountExpansions_TestClockIdWithoutExpansion_Throws()
    {
        var sub = new Subscription
        {
            Id = "sub_1",
            Customer = new Customer(),
            TestClockId = "clock_1",
            TestClock = null
        };

        Assert.Throws<InvalidOperationException>(() =>
            DiscountExtensions.RequireScheduleDiscountExpansions(sub, NullLogger.Instance));
    }

    [Fact]
    public void RequireScheduleDiscountExpansions_FullyExpanded_DoesNotThrow()
    {
        var sub = new Subscription
        {
            Id = "sub_1",
            Discounts = null,
            Customer = new Customer()
        };

        DiscountExtensions.RequireScheduleDiscountExpansions(sub, NullLogger.Instance);
    }
}
