using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Core.Billing.Constants;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class RetrievingPremiumSubscriptionTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task Subscription_ForActivePremiumUser_ReturnsTheCanonicalCartAndStorage()
    {
        var client = await fixture.PreparePremiumUserAsync("premium-subscription@example.com");

        // Drives GetBitwardenSubscriptionQuery.FetchSubscriptionAsync, which lists
        // schedules with Expand=phases.discounts.coupon.applies_to and reads the
        // subscription's items / coupons. Cart shape requires both expand paths
        // to be honored or the response would shed line items / discounts.
        var response = await client.GetAsync("/account/billing/vnext/subscription");
        await Assert.SuccessResponseAsync(response);

        var subscription = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("active", subscription["status"]!.GetValue<string>());
        Assert.NotNull(subscription["cart"]);
        Assert.NotNull(subscription["storage"]);
        Assert.NotNull(subscription["nextCharge"]);
    }

    [BillingFact]
    public async Task Subscription_IsCreatedWithClassicBillingMode()
    {
        // Premium creation (CreatePremiumCloudHostedSubscriptionCommand) sets
        // BillingMode = { Type = "classic" }. Verifies the mode lands on the Stripe subscription.
        const string email = "premium-billing-mode@example.com";
        await fixture.PreparePremiumUserAsync(email);

        var subscriptionId = await fixture.GetUserGatewaySubscriptionIdByEmailAsync(email);
        var billingModeType = await fixture.GetSubscriptionBillingModeTypeAsync(subscriptionId);

        Assert.Equal("classic", billingModeType);
    }

    [BillingFact]
    public async Task PaymentMethod_ForPremiumUser_ReturnsTheStripeVisaTestCard()
    {
        var client = await fixture.PreparePremiumUserAsync("premium-payment-method@example.com");

        var response = await client.GetAsync("/account/billing/vnext/payment-method");
        await Assert.SuccessResponseAsync(response);

        var paymentMethod = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("card", paymentMethod["type"]!.GetValue<string>());
        Assert.Equal("visa", paymentMethod["brand"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task Coupon_AppliesTo_RequiresExplicitExpansion()
    {
        // Documents the empirical Stripe rule that governs how deep the expand
        // paths in GetBitwardenSubscriptionQuery and StripePaymentService can
        // be truncated: `Coupon.applies_to` is an *expandable* sub-object, not
        // inline data. Without an explicit `.applies_to` in the expand list,
        // Stripe returns `applies_to: null` — even when the coupon was created
        // with a non-empty Products list.
        //
        // Consequence for our expand paths: any product-scoped coupon reached
        // through a truncated path (e.g. `customer.discount.source.coupon` at
        // 4 levels, without `.applies_to` because 5 levels is over Stripe's
        // cap) will carry `AppliesTo = null` in the response, and
        // GetBitwardenSubscriptionQuery.PartitionCouponsByScope will silently
        // misclassify it as cart-level. If Stripe ever flips this to inline,
        // this test flips too — and the truncations become safe.
        var couponId = $"applies_to_probe_{Guid.NewGuid():N}";
        var inline = await fixture.CreateAndReloadProductScopedCouponAsync(couponId);
        var expanded = await fixture.ReloadCouponWithAppliesToExpandedAsync(couponId);

        // With explicit expansion the products list is populated.
        Assert.NotNull(expanded);
        Assert.NotNull(expanded.Products);
        Assert.Contains(StripeConstants.ProductIDs.Premium, expanded.Products);

        // Without expansion Stripe returns applies_to as null. This is the
        // constraint that forces any expand path reaching a Coupon to include
        // `.applies_to` when the caller depends on AppliesTo.Products.
        Assert.Null(inline);
    }

    [BillingFact]
    public async Task Subscription_WithProductScopedSubscriptionCoupon_AttributesDiscountToSeatsCartItem()
    {
        // Regression coverage for GetBitwardenSubscriptionQuery's cart-attribution logic.
        // A subscription-level coupon whose `applies_to.products` includes the Premium
        // product must land on `cart.passwordManager.seats.discount` (product-level),
        // not on `cart.discount` (cart-level). If GetRelevantCouponsAsync's per-coupon
        // refetch step ever stopped populating Coupon.AppliesTo, PartitionCouponsByScope
        // would silently misclassify this coupon and this test would fail.
        var couponId = $"prod_scoped_sub_{Guid.NewGuid():N}";
        var client = await fixture.PreparePremiumUserWithProductScopedSubscriptionCouponAsync(
            "premium-sub-product-coupon@example.com", couponId);

        var response = await client.GetAsync("/account/billing/vnext/subscription");
        await Assert.SuccessResponseAsync(response);

        var subscription = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var cart = subscription["cart"]!;

        var seatsDiscount = cart["passwordManager"]!["seats"]!["discount"];
        Assert.NotNull(seatsDiscount);
        Assert.Equal("percent-off", seatsDiscount["type"]!.GetValue<string>());
        Assert.Equal(10m, seatsDiscount["value"]!.GetValue<decimal>());

        Assert.Null(cart["discount"]);
    }

    [BillingFact]
    public async Task Subscription_WithProductScopedPhase2ScheduleCoupon_AttributesDiscountToSeatsCartItem()
    {
        // Regression coverage for GetBitwardenSubscriptionQuery.GetSchedulePhase2CouponIdsAsync.
        // SubscriptionSchedulePhaseDiscount exposes `Coupon` directly (unlike
        // Subscription.Discounts[], which the 2025-09-30.clover refactor wrapped
        // under `Discount.Source.Coupon`). The SDK bump initially misapplied the
        // Source.Coupon pattern here — Stripe rejects `phases.discounts.source`
        // with a 500, and even if it didn't the reader `d.Discount.Source.Coupon`
        // wouldn't populate. The fix uses `phases.discounts.coupon.applies_to`
        // (4 levels, includes applies_to inline) with `d.Coupon` in the reader.
        var couponId = $"prod_scoped_phase2_{Guid.NewGuid():N}";
        var client = await fixture.PreparePremiumUserWithProductScopedPhase2CouponAsync(
            "premium-phase2-product-coupon@example.com", couponId);

        var response = await client.GetAsync("/account/billing/vnext/subscription");
        await Assert.SuccessResponseAsync(response);

        var subscription = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var cart = subscription["cart"]!;

        var seatsDiscount = cart["passwordManager"]!["seats"]!["discount"];
        Assert.NotNull(seatsDiscount);
        Assert.Equal("percent-off", seatsDiscount["type"]!.GetValue<string>());
        Assert.Equal(10m, seatsDiscount["value"]!.GetValue<decimal>());

        Assert.Null(cart["discount"]);
    }

    [BillingFact]
    public async Task Subscription_WithPlainPercentOffSubscriptionCoupon_AttributesDiscountToCartLevel()
    {
        // Complement of the product-scoped tests: a subscription-level coupon with NO applies_to
        // must land on cart-level `cart.discount`, not on a line item. Exercises the cart-level
        // branch of GetBitwardenSubscriptionQuery.PartitionCouponsByScope.
        const string email = "premium-plain-coupon@example.com";
        var client = await fixture.PreparePremiumUserAsync(email);
        var subscriptionId = await fixture.GetUserGatewaySubscriptionIdByEmailAsync(email);
        await fixture.SeedAndAttachSubscriptionCouponAsync(subscriptionId, $"plain_pct_{Guid.NewGuid():N}", percentOff: 15);

        var response = await client.GetAsync("/account/billing/vnext/subscription");
        await Assert.SuccessResponseAsync(response);

        var subscription = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var cart = subscription["cart"]!;

        var cartDiscount = cart["discount"];
        Assert.NotNull(cartDiscount);
        Assert.Equal("percent-off", cartDiscount["type"]!.GetValue<string>());
        Assert.Equal(15m, cartDiscount["value"]!.GetValue<decimal>());

        Assert.Null(cart["passwordManager"]!["seats"]!["discount"]);
    }
}
