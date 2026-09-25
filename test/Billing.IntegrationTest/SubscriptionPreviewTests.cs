using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class SubscriptionPreviewTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task Purchase_ForTeamsAnnually_ReturnsTaxAndTotal()
    {
        // Drives PreviewOrganizationTaxCommand.Run(user, purchase, billingAddress),
        // which builds an invoice preview for a to-be-purchased organization
        // subscription. No existing customer/subscription is needed — just an
        // authenticated user — so this covers the new-subscription preview branch
        // that PreExistingStateTests / PlanChange tests do not.
        var (token, _) = await fixture.Api.LoginWithNewAccount("preview-purchase@example.com");
        var client = fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            "/billing/preview-invoice/organizations/subscriptions/purchase",
            new
            {
                Purchase = new
                {
                    Tier = "Teams",
                    Cadence = "Annually",
                    PasswordManager = new { Seats = 10, AdditionalStorage = 0, Sponsored = false },
                },
                BillingAddress = new { Country = "US", PostalCode = "43432" },
            });
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(preview["tax"]);
        Assert.NotNull(preview["total"]);
    }

    [BillingFact]
    public async Task PlanChange_ToTeamsAnnually_ReturnsTaxAndTotal()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("preview-plan-change@example.com");

        var response = await client.PostAsJsonAsync(
            $"/billing/preview-invoice/organizations/{organizationId}/subscription/plan-change",
            new
            {
                Plan = new { Tier = "Teams", Cadence = "Annually" },
                BillingAddress = new { Country = "US", PostalCode = "43432" },
            });
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(preview["tax"]);
        Assert.NotNull(preview["total"]);
    }

    [BillingFact]
    public async Task SubscriptionUpdate_WithAdditionalSeats_ReturnsTaxAndTotal()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("preview-update@example.com");

        var response = await client.PutAsJsonAsync(
            $"/billing/preview-invoice/organizations/{organizationId}/subscription/update",
            new
            {
                Update = new
                {
                    PasswordManager = new { Seats = 15 },
                },
            });
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(preview["tax"]);
        Assert.NotNull(preview["total"]);
    }

    [BillingFact]
    public async Task PlanChange_WithCustomerLevelCoupon_AppliesCouponToPreviewTotal()
    {
        // Drives PreviewOrganizationTaxCommand.Run(org, planChange, ...), which
        // fetches the subscription with `Expand = ["customer"]` and — if the customer
        // has an active discount — carries `subscription.Customer.Discount.Source.Coupon.Id`
        // into the invoice preview's Discounts list. Regression coverage for the
        // customer.discount read after the SDK bump wrapped Coupon under Source.
        //
        // Customer-level coupon attach uses a raw-HTTP write with a pre-clover
        // Stripe-Version, since the typed CustomerUpdateOptions no longer exposes
        // `coupon`. The helper verifies the attach took effect before returning.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("preview-plan-change-customer-coupon@example.com");

        // Sanity baseline: preview without a coupon.
        var baselineResponse = await client.PostAsJsonAsync(
            $"/billing/preview-invoice/organizations/{organizationId}/subscription/plan-change",
            new
            {
                Plan = new { Tier = "Teams", Cadence = "Annually" },
                BillingAddress = new { Country = "US", PostalCode = "43432" },
            });
        await Assert.SuccessResponseAsync(baselineResponse);
        var baseline = (await baselineResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var baselineTotal = baseline["total"]!.GetValue<decimal>();

        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var couponId = $"cust_coupon_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, couponId, percentOff: 25);

        var discountedResponse = await client.PostAsJsonAsync(
            $"/billing/preview-invoice/organizations/{organizationId}/subscription/plan-change",
            new
            {
                Plan = new { Tier = "Teams", Cadence = "Annually" },
                BillingAddress = new { Country = "US", PostalCode = "43432" },
            });
        await Assert.SuccessResponseAsync(discountedResponse);
        var discounted = (await discountedResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var discountedTotal = discounted["total"]!.GetValue<decimal>();

        // The 25% customer coupon must reduce the preview total.
        Assert.True(discountedTotal < baselineTotal,
            $"Expected discounted total ({discountedTotal}) to be less than baseline ({baselineTotal})");
    }

    [BillingFact]
    public async Task PremiumPurchase_ReturnsTaxAndTotal()
    {
        // Drives PreviewPremiumTaxCommand.Run(user, preview, billingAddress),
        // which builds an invoice preview for a to-be-purchased premium
        // subscription. Covers the new-subscription preview branch that the
        // existing UpgradePreview test (existing premium sub) does not.
        var (token, _) = await fixture.Api.LoginWithNewAccount("preview-premium-purchase@example.com");
        var client = fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            "/billing/preview-invoice/premium/subscriptions/purchase",
            new
            {
                AdditionalStorage = (short)0,
                BillingAddress = new { Country = "US", PostalCode = "43432" },
            });
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(preview["tax"]);
        Assert.NotNull(preview["total"]);
    }
}
