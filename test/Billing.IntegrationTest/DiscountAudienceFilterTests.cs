using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class DiscountAudienceFilterTests(PersonalDiscountsFixture fixture) : IClassFixture<PersonalDiscountsFixture>
{
    [BillingFact]
    public async Task GetDiscounts_ForUserWithCustomerButNoPreviousPremium_ListsStripeSubsWithItemsPriceExpand()
    {
        const string email = "discount-filter-no-prior-premium@example.com";
        await fixture.SeedNoPreviousSubscriptionsDiscountAsync($"new_user_discount_{Guid.NewGuid():N}");

        // Register a user and attach a bare Stripe customer (no subscription) to satisfy
        // the filter's "user has GatewayCustomerId" branch — the listing then runs with
        // Expand=["data.items.data.price"].
        var (token, _) = await fixture.Api.LoginWithNewAccount(email);
        var client = fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var profileResponse = await client.GetAsync("/accounts/profile");
        await Assert.SuccessResponseAsync(profileResponse);
        var profile = (await profileResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var userId = profile["id"]!.GetValue<Guid>();

        await fixture.CreateOrphanedStripeCustomerForUserAsync(userId, email);

        var response = await client.GetAsync("/account/billing/vnext/discounts");
        await Assert.SuccessResponseAsync(response);
    }
}
