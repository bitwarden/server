using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Previews Premium and organization purchases against Stripe test mode. Addresses use New York
/// (12345) so Stripe computes a non-zero tax.
/// </summary>
public class PreviewingAccountPurchaseTests(PreviewDrivenCartFixture fixture)
    : IClassFixture<PreviewDrivenCartFixture>
{
    private const string PremiumRoute = "/account/billing/subscription/purchase/preview";
    private const string OrganizationRoute = "/account/billing/subscription/purchase/organization/preview";

    [BillingFact]
    public async Task PremiumPurchase_WithoutStorage_PreviewsThePremiumSeatWithTax()
    {
        var client = await CreateClientAsync("premium-purchase-preview");
        var premiumPlan = await WithPricingClientAsync(pricingClient => pricingClient.GetAvailablePremiumPlan());

        var response = await client.GetAsync($"{PremiumRoute}?additionalStorage=0&country=US&postalCode=12345");

        await Assert.SuccessResponseAsync(response);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var preview = await ReadAsync(response);
        Assert.Equal("premium", preview["planTier"]!.GetValue<string>());
        var seats = preview["passwordManager"]!["seats"]!;
        Assert.Equal(StripeConstants.PurchasableReferences.PasswordManagerSeat, seats["reference"]!.GetValue<string>());
        Assert.Equal(1, seats["quantity"]!.GetValue<long>());
        Assert.Equal(premiumPlan.Seat.Price, seats["cost"]!.GetValue<decimal>());
        Assert.Equal(seats["cost"]!.GetValue<decimal>() + Decimal(preview, "estimatedTax"), Decimal(preview, "total"));
    }

    [BillingFact]
    public async Task PremiumPurchase_WithStorage_PreviewsTheStorageLine()
    {
        var client = await CreateClientAsync("premium-purchase-storage-preview");

        var response = await client.GetAsync($"{PremiumRoute}?additionalStorage=2&country=US&postalCode=12345");

        await Assert.SuccessResponseAsync(response);
        var preview = await ReadAsync(response);
        var passwordManager = preview["passwordManager"]!;
        var storage = passwordManager["additionalStorage"]!;
        Assert.Equal(2, storage["quantity"]!.GetValue<long>());
        var subtotal = passwordManager["seats"]!["cost"]!.GetValue<decimal>()
            + storage["cost"]!.GetValue<decimal>() * 2;
        Assert.Equal(subtotal + Decimal(preview, "estimatedTax"), Decimal(preview, "total"));
    }

    [BillingFact]
    public async Task PremiumPurchase_WithAnEligiblePremiumCoupon_AppliesTheDiscount()
    {
        var couponId = await fixture.SeedNoPreviousSubscriptionsDiscountAsync($"premium_purchase_preview_{Guid.NewGuid():N}");
        var client = await CreateClientAsync("premium-purchase-coupon-preview");

        var response = await client.GetAsync(
            $"{PremiumRoute}?additionalStorage=0&coupons={couponId}&country=US&postalCode=12345");

        await Assert.SuccessResponseAsync(response);
        var preview = await ReadAsync(response);
        Assert.True(HasAnyDiscount(preview), "Expected the eligible Premium coupon to be projected as a discount.");
        var seatCost = preview["passwordManager"]!["seats"]!["cost"]!.GetValue<decimal>();
        Assert.True(Decimal(preview, "total") - Decimal(preview, "estimatedTax") < seatCost);
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForTeamsAnnually_PreviewsTheSeats()
    {
        var client = await CreateClientAsync("teams-purchase-preview");

        var response = await client.PostAsJsonAsync(OrganizationRoute,
            Body("Teams", "Annually", seats: 10));

        await Assert.SuccessResponseAsync(response);
        var preview = await ReadAsync(response);
        Assert.Equal("teams", preview["planTier"]!.GetValue<string>());
        var seats = preview["passwordManager"]!["seats"]!;
        Assert.Equal(10, seats["quantity"]!.GetValue<long>());
        Assert.Equal(seats["cost"]!.GetValue<decimal>() * 10 + Decimal(preview, "estimatedTax"), Decimal(preview, "total"));
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForEnterpriseMonthlyWithSecretsManager_PreviewsBothProducts()
    {
        var client = await CreateClientAsync("enterprise-sm-purchase-preview");

        var response = await client.PostAsJsonAsync(OrganizationRoute,
            Body("Enterprise", "Monthly", seats: 5,
                secretsManager: new { seats = 5, additionalServiceAccounts = 10, standalone = false }));

        await Assert.SuccessResponseAsync(response);
        var preview = await ReadAsync(response);
        Assert.Equal("monthly", preview["cadence"]!.GetValue<string>());
        var secretsManager = preview["secretsManager"]!;
        Assert.Equal(5, secretsManager["seats"]!["quantity"]!.GetValue<long>());
        Assert.Equal(10, secretsManager["additionalServiceAccounts"]!["quantity"]!.GetValue<long>());
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForFamilies_PreviewsOnePackageAtTheCatalogPrice()
    {
        var client = await CreateClientAsync("families-purchase-preview");
        var familiesPlan = await WithPricingClientAsync(pricingClient => pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually));

        var response = await client.PostAsJsonAsync(OrganizationRoute, Body("Families", "Annually", seats: 6));

        await Assert.SuccessResponseAsync(response);
        var preview = await ReadAsync(response);
        Assert.Equal("families", preview["planTier"]!.GetValue<string>());
        var seats = preview["passwordManager"]!["seats"]!;
        Assert.Equal(1, seats["quantity"]!.GetValue<long>());
        Assert.Equal(familiesPlan.PasswordManager.BasePrice, seats["cost"]!.GetValue<decimal>());
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForFamiliesWithAnEligibleFamiliesCoupon_AppliesTheDiscount()
    {
        var couponId = await fixture.SeedNoPreviousSubscriptionsDiscountAsync(
            $"families_purchase_preview_{Guid.NewGuid():N}", [StripeConstants.ProductIDs.Families]);
        var client = await CreateClientAsync("families-purchase-coupon-preview");

        var response = await client.PostAsJsonAsync(OrganizationRoute,
            Body("Families", "Annually", seats: 1, coupons: [couponId]));

        await Assert.SuccessResponseAsync(response);
        var preview = await ReadAsync(response);
        Assert.True(HasAnyDiscount(preview), "Expected the eligible Families coupon to be projected as a discount.");
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForSponsoredFamilies_ReturnsStripesProjectionOfTheSponsoredPrice()
    {
        var client = await CreateClientAsync("sponsored-families-purchase-preview");

        var response = await client.PostAsJsonAsync(OrganizationRoute,
            Body("Families", "Annually", seats: 1, sponsored: true));

        await Assert.SuccessResponseAsync(response);
        var preview = await ReadAsync(response);
        var seats = preview["passwordManager"]!["seats"]!;
        Assert.Equal(StripeConstants.PurchasableReferences.PasswordManagerSeat, seats["reference"]!.GetValue<string>());
        Assert.Equal(1, seats["quantity"]!.GetValue<long>());
        Assert.Equal(Decimal(preview, "total"), Decimal(preview, "amountDue"));
        Assert.True(preview["discounts"] is null or JsonArray { Count: 0 });
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForSponsoredFamiliesWithStorage_CarriesTheStorageLine()
    {
        var client = await CreateClientAsync("sponsored-families-storage-purchase-preview");

        var response = await client.PostAsJsonAsync(OrganizationRoute,
            Body("Families", "Annually", seats: 1, sponsored: true, additionalStorage: 2));

        await Assert.SuccessResponseAsync(response);
        var preview = await ReadAsync(response);
        Assert.Equal(StripeConstants.PurchasableReferences.PasswordManagerSeat,
            preview["passwordManager"]!["seats"]!["reference"]!.GetValue<string>());
        var storage = preview["passwordManager"]!["additionalStorage"]!;
        Assert.Equal(StripeConstants.PurchasableReferences.PasswordManagerStorage, storage["reference"]!.GetValue<string>());
        Assert.Equal(2, storage["quantity"]!.GetValue<long>());
        Assert.True(Decimal(preview, "total") > 0m);
    }

    [BillingFact]
    public async Task SponsoredFamiliesPrice_CarriesThePasswordManagerSeatReference()
    {
        var metadata = await fixture.GetPriceMetadataAsync(
            SponsoredPlans.Get(PlanSponsorshipType.FamiliesForEnterprise).StripePlanId);

        Assert.Equal(StripeConstants.PurchasableReferences.PasswordManagerSeat,
            metadata.TryGetValue(StripeConstants.MetadataKeys.PurchasableReference, out var reference) ? reference : null);
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForMonthlyFamilies_Returns400()
    {
        var client = await CreateClientAsync("monthly-families-purchase-preview");

        var response = await client.PostAsJsonAsync(OrganizationRoute, Body("Families", "Monthly", seats: 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForFamiliesWithSecretsManager_Returns400()
    {
        var client = await CreateClientAsync("families-sm-purchase-preview");

        var response = await client.PostAsJsonAsync(OrganizationRoute,
            Body("Families", "Annually", seats: 1,
                secretsManager: new { seats = 1, additionalServiceAccounts = 0, standalone = false }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [BillingFact]
    public async Task OrganizationPurchase_ForEnterpriseWithAGermanVatId_ReachesStripe()
    {
        var client = await CreateClientAsync("enterprise-vat-purchase-preview");

        var response = await client.PostAsJsonAsync(OrganizationRoute, new
        {
            purchase = new
            {
                tier = "Enterprise",
                cadence = "Annually",
                passwordManager = new { seats = 5, additionalStorage = 0, sponsored = false }
            },
            billingAddress = new
            {
                country = "DE",
                postalCode = "10115",
                taxId = new { code = "eu_vat", value = "DE123456789" }
            }
        });

        await Assert.SuccessResponseAsync(response);
    }

    private async Task<HttpClient> CreateClientAsync(string emailPrefix)
    {
        var (token, _) = await fixture.Api.LoginWithNewAccount($"{emailPrefix}-{Guid.NewGuid():N}@example.com");
        var client = fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<T> WithPricingClientAsync<T>(Func<IPricingClient, Task<T>> read)
    {
        using var scope = fixture.Api.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<IPricingClient>());
    }

    private static object Body(
        string tier, string cadence, int seats, bool sponsored = false, object? secretsManager = null, string[]? coupons = null,
        int additionalStorage = 0) => new
        {
            purchase = new
            {
                tier,
                cadence,
                passwordManager = new { seats, additionalStorage, sponsored },
                secretsManager,
                coupons
            },
            billingAddress = new { country = "US", postalCode = "12345" }
        };

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonNode>())!;

    private static decimal Decimal(JsonNode preview, string property) => preview[property]!.GetValue<decimal>();

    // A coupon with no applies-to lands on the cart; a product-scoped one lands on the seat line.
    private static bool HasAnyDiscount(JsonNode preview) =>
        preview["discounts"] is JsonArray { Count: > 0 }
        || preview["passwordManager"]!["seats"]!["discounts"] is JsonArray { Count: > 0 };
}
