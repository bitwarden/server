namespace Bit.Billing.IntegrationTest;

public class UnpaidCancellationTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task ScheduleUnpaidCancellation_FetchesSubscriptionWithTestClockExpanded()
    {
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("schedule-unpaid-cancel@example.com");

        // Drives ScheduleUnpaidCancellationAsync -> GetSubscription with Expand=["test_clock"].
        // The subsequent status guard short-circuits because the sub isn't actually Unpaid,
        // but the Expand-using fetch has already executed.
        await fixture.ScheduleUnpaidCancellationForOrganizationAsync(organizationId);
    }

    [BillingFact]
    public async Task ResumeUnpaidCancellation_FetchesSubscriptionWithCustomerDiscountExpanded()
    {
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("resume-unpaid-cancel@example.com");
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);

        // Attach a customer- and a subscription-level discount so the fetch runs against a discounted
        // subscriber. ResumeFromUnpaidCancellationAsync's ScheduleForSubscription reads
        // Customer.Discount.Source.Coupon and Discounts[].Source.Coupon, which the 2025-09-30.clover
        // refactor moved under Source.
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, $"resume_cust_{Guid.NewGuid():N}", percentOff: 15);
        await fixture.SeedAndAttachSubscriptionCouponAsync(subscriptionId, $"resume_sub_{Guid.NewGuid():N}", percentOff: 10);

        // Drives ResumeFromUnpaidCancellationAsync -> GetSubscription with
        // Expand=["customer.discount.source.coupon", "discounts.source.coupon"]. The status guard
        // short-circuits (the org isn't Unpaid), but the discount-expanding fetch has already run —
        // if either path exceeded Stripe's 4-level cap this would 400 and fail the test.
        await fixture.ResumeFromUnpaidCancellationForOrganizationAsync(organizationId);
    }
}
