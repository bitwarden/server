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
