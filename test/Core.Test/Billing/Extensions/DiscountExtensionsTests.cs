using Bit.Core.Billing.Extensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Extensions;

public class DiscountExtensionsTests
{
    private static Discount CustomerDiscount(string couponId, bool valid = true) =>
        new() { Coupon = new Coupon { Id = couponId, Valid = valid } };

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
        var result = DiscountExtensions.MergeDiscountCouponIds(new Discount { Coupon = null }, ["a"]);

        Assert.Equal(["a"], result);
    }

    [Fact]
    public void MergeDiscountCouponIds_CustomerCouponWithNullId_NoThrow_NoEntry()
    {
        var result = DiscountExtensions.MergeDiscountCouponIds(
            new Discount { Coupon = new Coupon { Id = null } }, ["a"]);

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
}
