using Bit.Core.Billing.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Extensions;

public class DiscountExtensionsTests
{
    private static Discount CustomerDiscount(string couponId, bool valid = true) =>
        new() { Source = new DiscountSource { Coupon = new Coupon { Id = couponId, Valid = valid } } };

    [Fact]
    public void MergeDiscountCouponIds_CustomerDiscountOnly_ReturnsCustomerCoupon()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(CustomerDiscount("customer"), null);

        Assert.Equal(["customer"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_ExistingDiscountsOnly_ReturnsThemInOrder()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(null, ["a", "b", "c"]);

        Assert.Equal(["a", "b", "c"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_AllDistinct_PreservesCustomerFirstThenExistingThenNew()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(
            CustomerDiscount("customer"), ["a", "b"], "new");

        Assert.Equal(["customer", "a", "b", "new"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_CustomerCouponAlsoInExistingDiscounts_AppearsOnce()
    {
        // Closes the reference-pattern dedup gap: a coupon present on BOTH the customer and the
        // subscription must not be double-added (Stripe does not dedupe identical coupons).
        var result = DiscountExtensions.MergeDiscountCouponIds(
            CustomerDiscount("shared"), ["shared", "other"], "new");

        Assert.Equal(["shared", "other", "new"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_NewCouponAlreadyInExisting_AppearsOnce()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(null, ["a", "new"], "new");

        Assert.Equal(["a", "new"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_InvalidCustomerCoupon_StillCarried()
    {
        // Validity is intentionally NOT gated: an active customer.discount is one Stripe is already
        // applying. Pin this so a future re-introduction of IsValid()/Coupon.Valid filtering is caught.
        var result = DiscountExtensions.MergeDiscountCouponIds(
            CustomerDiscount("customer", valid: false), null, "new");

        Assert.Equal(["customer", "new"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_NullCustomerDiscount_NoCustomerEntry()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(null, ["a"], "new");

        Assert.Equal(["a", "new"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_CustomerDiscountWithNullCoupon_NoThrow_NoEntry()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(new Discount { Source = null }, ["a"]);

        Assert.Equal(["a"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_CustomerCouponWithNullId_NoThrow_NoEntry()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(
            new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = null } } }, ["a"]);

        Assert.Equal(["a"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_NullAndEmptyInterleavedExisting_SkippedWithoutBreakingOrder()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(
            CustomerDiscount("customer"), [null, "a", "", "b", null], "new");

        Assert.Equal(["customer", "a", "b", "new"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_EmptyEverything_ReturnsEmpty()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(null, null);

        Assert.Empty(result);
    }

    [Fact]
    public void MergeDiscountCouponIds_OrdinalCaseSensitive_DistinctEntries()
    {
        // Stripe coupon IDs are case-sensitive, so "Coupon" and "coupon" are distinct.
        var result = DiscountExtensions.MergeDiscountCouponIds(null, ["Coupon", "coupon"]);

        Assert.Equal(["Coupon", "coupon"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_PinsFullOrderedOutput_CustomerFirst()
    {
        // Lock the customer-first precedence so a refactor can't silently reorder. The dollar total
        // for amount_off x percent_off is order-sensitive, so this ordering is load-bearing.
        var result = DiscountExtensions.MergeDiscountCouponIds(
            CustomerDiscount("customer"), ["a", "b"], "new");

        Assert.Equal(4, result.Count);
        Assert.Equal("customer", result[0]);
        Assert.Equal("a", result[1]);
        Assert.Equal("b", result[2]);
        Assert.Equal("new", result[3]);
    }

    [Fact]
    public void MergeDiscountCouponIds_MultipleNewCoupons_AppendedInOrder()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(null, ["a"], "n1", null, "n2");

        Assert.Equal(["a", "n1", "n2"], result);
    }

    [Fact]
    public void ToSubscriptionDiscountOptions_ProjectsOneToOnePreservingOrder()
    {
        var result = new[] { "a", "b", "c" }.ToSubscriptionDiscountOptions();

        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0].Coupon);
        Assert.Equal("b", result[1].Coupon);
        Assert.Equal("c", result[2].Coupon);
    }

    [Fact]
    public void ToPhaseDiscountOptions_ProjectsOneToOnePreservingOrder()
    {
        var result = new[] { "a", "b", "c" }.ToPhaseDiscountOptions();

        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0].Coupon);
        Assert.Equal("b", result[1].Coupon);
        Assert.Equal("c", result[2].Coupon);
    }

    [Fact]
    public void ToPhaseDiscountOptions_EmptyList_ReturnsEmpty()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(null, null).ToPhaseDiscountOptions();

        Assert.Empty(result);
    }

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
