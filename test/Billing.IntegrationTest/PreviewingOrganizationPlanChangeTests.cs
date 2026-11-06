using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Core.Billing.Enums;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Previews organization plan changes against Stripe test mode. The organization starts on Teams
/// (Annually) and previews a move up to Enterprise (Annually).
/// </summary>
public class PreviewingOrganizationPlanChangeTests(StripeTestsFixture fixture)
    : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task PlanChange_ToEnterpriseAnnually_ReturnsTheEnterpriseCartWithTax()
    {
        var (client, _, organizationId, _) = await fixture.PrepareOrganizationOwnerAsync(
            "preview-plan-change@example.com", PlanType.TeamsAnnually);

        var response = await client.PostAsJsonAsync(Route(organizationId), Body("enterprise", "annually"));

        await Assert.SuccessResponseAsync(response);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("enterprise", preview["planTier"]!.GetValue<string>());
        Assert.Equal("annually", preview["cadence"]!.GetValue<string>());
        Assert.True(preview["passwordManager"]!["seats"]!["quantity"]!.GetValue<long>() > 0);
        Assert.True(preview["total"]!.GetValue<decimal>() > 0m);
        Assert.True(preview["estimatedTax"]!.GetValue<decimal>() >= 0m);
        Assert.NotNull(preview["amountDue"]);
    }

    [BillingFact]
    public async Task PlanChange_WithCustomerLevelCoupon_AppliesCouponToPreviewTotal()
    {
        // The command reads customer.discount when it builds the preview, so this guards the Expand
        // path. The coupon is attached with a raw-HTTP write (the typed CustomerUpdateOptions no
        // longer exposes `coupon`); the helper verifies the attach took effect.
        var (client, _, organizationId, _) = await fixture.PrepareOrganizationOwnerAsync(
            "preview-plan-change-customer-coupon@example.com", PlanType.TeamsAnnually);

        var baselineResponse = await client.PostAsJsonAsync(Route(organizationId), Body("enterprise", "annually"));
        await Assert.SuccessResponseAsync(baselineResponse);
        var baseline = (await baselineResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var baselineTotal = baseline["total"]!.GetValue<decimal>();

        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var couponId = $"cust_coupon_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, couponId, percentOff: 25);

        var discountedResponse = await client.PostAsJsonAsync(Route(organizationId), Body("enterprise", "annually"));
        await Assert.SuccessResponseAsync(discountedResponse);
        var discounted = (await discountedResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var discountedTotal = discounted["total"]!.GetValue<decimal>();

        Assert.True(discountedTotal < baselineTotal,
            $"Expected discounted total ({discountedTotal}) to be less than baseline ({baselineTotal})");
    }

    [BillingFact]
    public async Task PlanChange_PaymentOptionalTrialWithoutPaymentMethod_ReturnsPreviewWithPositiveTotal()
    {
        var (client, _, organizationId, _) = await fixture.PrepareOrganizationOwnerAsync(
            "preview-plan-change-payment-optional-trial@example.com", PlanType.TeamsAnnually);
        await fixture.MakeOrganizationTrialPaymentOptionalAsync(organizationId);

        var response = await client.PostAsJsonAsync(Route(organizationId), Body("enterprise", "annually"));

        await Assert.SuccessResponseAsync(response);
        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("enterprise", preview["planTier"]!.GetValue<string>());
        Assert.True(preview["total"]!.GetValue<decimal>() > 0m);
    }

    [BillingFact]
    public async Task PlanChange_TrialWithCustomerAndSubscriptionCoupons_AppliesOnlySubscriptionCoupon()
    {
        // Verifies in real Stripe that a subscription's own discount takes precedence over the customer's.
        var (client, _, organizationId, _) = await fixture.PrepareOrganizationOwnerAsync(
            "preview-plan-change-coupon-precedence@example.com", PlanType.TeamsAnnually);
        await fixture.MakeOrganizationTrialPaymentOptionalAsync(organizationId);
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);

        await fixture.SeedAndAttachSubscriptionCouponAsync(subscriptionId, $"sub_coupon_{Guid.NewGuid():N}", percentOff: 50);
        var subscriptionOnly = await PreviewTotalAsync(client, organizationId);

        await fixture.SeedAndAttachCustomerCouponAsync(customerId, $"cust_coupon_{Guid.NewGuid():N}", percentOff: 25);
        var both = await PreviewTotalAsync(client, organizationId);

        Assert.Equal(subscriptionOnly, both);
    }

    private static async Task<decimal> PreviewTotalAsync(HttpClient client, Guid organizationId)
    {
        var response = await client.PostAsJsonAsync(Route(organizationId), Body("enterprise", "annually"));
        await Assert.SuccessResponseAsync(response);
        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        return preview["total"]!.GetValue<decimal>();
    }

    private static string Route(Guid organizationId) =>
        $"/organizations/{organizationId}/billing/subscription/plan-change/preview";

    private static object Body(string tier, string cadence) => new
    {
        tier,
        cadence,
        billingAddress = new { country = "US", postalCode = "12345" }
    };
}
