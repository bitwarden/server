using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class MigrationCohortTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task ChurnMitigationOffer_FetchesSubscriptionWithCustomerTestClockDiscountsExpand()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("churn-offer@example.com");
        await fixture.SeedChurnOnlyCohortAsync(organizationId, $"churn_offer_{Guid.NewGuid():N}");

        // Drives GetChurnMitigationOfferQuery -> TryGetSubscriptionAsync with
        // Expand=["customer", "test_clock", "discounts.coupon"].
        var response = await client.GetAsync(
            $"/organizations/{organizationId}/billing/vnext/churn-mitigation-offer");
        await Assert.SuccessResponseAsync(response);
    }

    [BillingFact]
    public async Task RedeemChurnMitigationOffer_FetchesSubscriptionWithSameExpand()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("churn-redeem@example.com");
        await fixture.SeedChurnOnlyCohortAsync(organizationId, $"churn_redeem_{Guid.NewGuid():N}");

        // Drives RedeemChurnMitigationOfferCommand which fetches the subscription with
        // the same Expand.
        var response = await client.PostAsync(
            $"/organizations/{organizationId}/billing/vnext/churn-mitigation-offer/redeem",
            content: null);
        await Assert.SuccessResponseAsync(response);
    }

    [BillingFact]
    public async Task ChurnMitigationOffer_WhenCustomerAlreadyHasCoupon_IsIneligible()
    {
        // GetChurnMitigationOfferQuery.EvaluateChurnOnlyCohortAsync inspects
        // subscription.Customer.Discount.Source.Coupon.Id and treats an already-
        // matching coupon as ineligible. That read requires
        // `customer.discount.source.coupon` in the subscription fetch's expand;
        // without it, Source is null and the check silently passes, offering
        // the same coupon twice — potential double-application.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("churn-offer-customer-has@example.com");
        var couponId = $"churn_cust_{Guid.NewGuid():N}";
        await fixture.SeedChurnOnlyCohortAsync(organizationId, couponId);

        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        // Recreate the coupon (SeedChurnOnlyCohortAsync doesn't attach it — it just
        // registers the id in the cohort). AttachCustomerCouponAsync uses raw HTTP
        // with legacy Stripe-Version.
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, couponId, percentOff: 25);

        var response = await client.GetAsync(
            $"/organizations/{organizationId}/billing/vnext/churn-mitigation-offer");
        await Assert.SuccessResponseAsync(response);

        // Endpoint uses TypedResults.Ok(offer); ASP.NET Core serializes a null offer
        // as an empty response body. Non-empty body means an offer was returned.
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrEmpty(body) || body == "null",
            $"Expected null offer (customer already has the churn coupon) but got: '{body}'");
    }

    [BillingFact]
    public async Task ChurnMitigationOffer_WhenSubscriptionAlreadyHasCoupon_IsIneligible()
    {
        // Same eligibility check but on the subscription-level branch — reads
        // d.Source.Coupon.Id off subscription.Discounts. Requires the expand
        // to be `discounts.source.coupon`, not the pre-clover `discounts.coupon`.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("churn-offer-sub-has@example.com");
        var couponId = $"churn_sub_{Guid.NewGuid():N}";
        await fixture.SeedChurnOnlyCohortAsync(organizationId, couponId);

        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);
        await fixture.SeedAndAttachSubscriptionCouponAsync(subscriptionId, couponId, percentOff: 25);

        var response = await client.GetAsync(
            $"/organizations/{organizationId}/billing/vnext/churn-mitigation-offer");
        await Assert.SuccessResponseAsync(response);

        // Endpoint uses TypedResults.Ok(offer); ASP.NET Core serializes a null offer
        // as an empty response body. Non-empty body means an offer was returned.
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrEmpty(body) || body == "null",
            $"Expected null offer (subscription already has the churn coupon) but got: '{body}'");
    }

    [BillingFact]
    public async Task RedeemChurnMitigationOffer_WithExistingSubscriptionDiscount_MergesInsteadOfNREing()
    {
        // RedeemForChurnOnlyCohortAsync reads `subscription.Discounts?.Select(d =>
        // d.Source.Coupon.Id)` — NOT null-safe. Without `discounts.source.coupon`
        // in the expand, d.Source is null and the redeem 500s with an NRE. Seed a
        // separate (non-churn) coupon on the subscription and verify redeem still
        // succeeds and merges both coupons.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("churn-redeem-with-existing-sub@example.com");
        var couponId = $"churn_redeem_{Guid.NewGuid():N}";
        await fixture.SeedChurnOnlyCohortAsync(organizationId, couponId);

        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);
        var preExistingCouponId = $"pre_existing_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachSubscriptionCouponAsync(subscriptionId, preExistingCouponId, percentOff: 5);

        var response = await client.PostAsync(
            $"/organizations/{organizationId}/billing/vnext/churn-mitigation-offer/redeem",
            content: null);
        await Assert.SuccessResponseAsync(response);
    }

    [BillingFact]
    public async Task RedeemChurnMitigationOffer_WithCustomerDiscount_PreservesItInMergedSet()
    {
        // RedeemForChurnOnlyCohortAsync passes subscription.Customer?.Discount into
        // MergeDiscountCouponIds, which reads `customerDiscount?.Source?.Coupon?.Id`.
        // Without `customer.discount.source.coupon` in the expand, Source is null
        // and the customer coupon silently drops from the merged set — the redeem
        // succeeds (200) but the customer coupon is lost on the subscription's
        // Discounts list, so Stripe's sub-overrides-customer stacking behavior
        // effectively cancels the customer discount.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("churn-redeem-customer-discount@example.com");
        var couponId = $"churn_cust_merge_{Guid.NewGuid():N}";
        await fixture.SeedChurnOnlyCohortAsync(organizationId, couponId);

        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var customerCouponId = $"cust_pre_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, customerCouponId, percentOff: 5);

        var response = await client.PostAsync(
            $"/organizations/{organizationId}/billing/vnext/churn-mitigation-offer/redeem",
            content: null);
        await Assert.SuccessResponseAsync(response);

        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);
        var subCoupons = await fixture.GetSubscriptionDiscountCouponIdsAsync(subscriptionId);

        Assert.Contains(customerCouponId, subCoupons);
        Assert.Contains(couponId, subCoupons);
    }

    [BillingFact]
    public async Task SubscriptionRead_WithMigrationScheduleAndCohort_ReadsSchedulePhase2()
    {
        // Premium-user subscription endpoint that reads the schedule's Phase 2 coupons —
        // exercises GetBitwardenSubscriptionQuery.GetSchedulePhase2CouponsAsync with
        // Expand=["phases.discounts.coupon.applies_to"].
        // We seed it against an organization, then read /organizations/{id}/subscription
        // which exercises the legacy StripePaymentService schedule Expand.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("schedule-phase2-org@example.com");
        await fixture.SeedMigrationCohortWithScheduleAsync(organizationId, MigrationPathId.Enterprise2020AnnualToCurrent);

        var response = await client.GetAsync($"/organizations/{organizationId}/subscription");
        await Assert.SuccessResponseAsync(response);
    }
}
