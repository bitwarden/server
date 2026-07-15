using System.Net.Http.Json;
using Bit.Core.Billing.Enums;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Regression coverage for the customer-level discount surviving a subscription mutation that rebuilds
/// an active deferred-migration schedule's future phase. The 2025-09-30.clover refactor moved the
/// coupon under <c>Discount.Source.Coupon</c>; if a mutation command fetches the subscription/customer
/// without the <c>customer.discount.source.coupon</c> / <c>discount.source.coupon</c> expand, the
/// customer coupon reads <c>null</c> and is silently dropped from the rebuilt Phase 2 — a failure a
/// mocked unit test cannot see (mocks return the coupon regardless of the expand).
///
/// Template for gap 5 (<c>UpdatePremiumStorageCommand</c>). Once validated against Stripe, the same
/// pattern replicates to gap 4 (<c>UpdateOrganizationSubscriptionCommand</c>) and gap 6
/// (<c>UpdateBillingAddressCommand</c>, personal + business).
/// </summary>
public class PreservingDiscountsThroughScheduleTests(DeferredPriceMigrationFixture fixture)
    : IClassFixture<DeferredPriceMigrationFixture>
{
    [BillingFact]
    public async Task StorageChange_WithActiveScheduleAndCustomerCoupon_PreservesCouponInFuturePhase()
    {
        const string email = "storage-schedule-coupon@example.com";
        var client = await fixture.PreparePremiumUserAsync(email);
        var customerId = await fixture.GetUserGatewayCustomerIdByEmailAsync(email);
        var subscriptionId = await fixture.GetUserGatewaySubscriptionIdByEmailAsync(email);

        // Put the premium subscription on the legacy price so PriceIncreaseScheduler will defer a
        // migration, then create the real active 2-phase schedule via the scheduler itself.
        await fixture.MovePremiumSubscriptionToLegacyPriceAsync(subscriptionId);
        await fixture.CreateDeferredPriceIncreaseScheduleAsync(subscriptionId);

        // Attach the customer coupon AFTER the schedule exists, so Phase 2 does not already carry it.
        // The only way it can survive the storage-change schedule rebuild is via the customer-discount
        // carry that the customer.discount.source.coupon expand feeds; without the expand it reads null
        // and is dropped.
        var couponId = $"storage_cust_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, couponId, percentOff: 10);

        // Guard against a false pass: the coupon must NOT be in Phase 2 before the mutation.
        var couponsBefore = await fixture.GetSchedulePhaseCouponIdsAsync(subscriptionId, phaseIndex: 1);
        Assert.DoesNotContain(couponId, couponsBefore);

        // Drive UpdatePremiumStorageCommand, which rebuilds the schedule's future phase.
        var response = await client.PutAsJsonAsync(
            "/account/billing/vnext/subscription/storage",
            new { AdditionalStorageGb = (short)1 });
        await Assert.SuccessResponseAsync(response);

        // The rebuilt future phase must still carry the customer-level coupon.
        var couponsAfter = await fixture.GetSchedulePhaseCouponIdsAsync(subscriptionId, phaseIndex: 1);
        Assert.Contains(couponId, couponsAfter);
    }

    [BillingFact]
    public async Task PersonalAddressChange_WithActiveScheduleAndCustomerCoupon_PreservesCouponInFuturePhase()
    {
        const string email = "families-address-schedule-coupon@example.com";
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync(email, PlanType.FamiliesAnnually);
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);

        // Families is a personal-tier product; move it onto the legacy families price so the scheduler's
        // families branch defers a migration, then create the real 2-phase schedule.
        await fixture.MoveFamiliesSubscriptionToLegacyPriceAsync(subscriptionId);
        await fixture.CreateDeferredPriceIncreaseScheduleAsync(subscriptionId);

        // Attach the customer coupon after the schedule exists (see storage test) so the only way it
        // survives the address-change schedule rebuild is the customer-discount carry that the personal
        // path's discount.source.coupon expand feeds.
        var couponId = $"personal_addr_cust_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, couponId, percentOff: 10);

        var couponsBefore = await fixture.GetSchedulePhaseCouponIdsAsync(subscriptionId, phaseIndex: 1);
        Assert.DoesNotContain(couponId, couponsBefore);

        // Families routes UpdateBillingAddressCommand through UpdatePersonalBillingAddressAsync, whose
        // EnableAutomaticTaxAsync rebuilds the active schedule's future phase.
        var response = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/billing/vnext/address",
            new
            {
                Country = "US",
                PostalCode = "10001",
                Line1 = "1 Family Way",
                City = "New York",
                State = "NY",
            });
        await Assert.SuccessResponseAsync(response);

        var couponsAfter = await fixture.GetSchedulePhaseCouponIdsAsync(subscriptionId, phaseIndex: 1);
        Assert.Contains(couponId, couponsAfter);
    }
}
