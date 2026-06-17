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

        // Drives ResumeFromUnpaidCancellationAsync -> GetSubscription with
        // Expand=["customer.discount", "discounts"].
        await fixture.ResumeFromUnpaidCancellationForOrganizationAsync(organizationId);
    }
}
