using System.Net.Http.Json;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class CancellingAndReinstatingSubscriptionTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task CancelPremium_FetchesSubscriptionWithTestClockExpanded()
    {
        var client = await fixture.PreparePremiumUserAsync("cancel-premium@example.com");

        // Drives SubscriberService.CancelSubscription -> GetSubscriptionOrThrow with
        // Expand=["test_clock"].
        var response = await client.PostAsJsonAsync(
            "/accounts/cancel",
            new { Reason = "user_test", Feedback = "integration test cancellation" });
        await Assert.SuccessResponseAsync(response);
    }

    [BillingFact]
    public async Task Reinstate_FetchesCanceledSubscriptionAndCreatesReplacement()
    {
        var client = await fixture.PreparePremiumUserAsync("reinstate-premium@example.com");

        // Cancel first so reinstate has something to act on.
        var cancelResponse = await client.PostAsJsonAsync(
            "/accounts/cancel",
            new { Reason = "user_test", Feedback = "" });
        await Assert.SuccessResponseAsync(cancelResponse);

        // Drives ReinstateSubscriptionCommand which fetches the canceled subscription
        // with Expand=["discounts", "customer.discount"].
        var reinstateResponse = await client.PostAsync(
            "/account/billing/vnext/subscription/reinstate", content: null);
        await Assert.SuccessResponseAsync(reinstateResponse);
    }
}
