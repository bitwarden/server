using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Xunit;

namespace Bit.Api.IntegrationTest.Billing;

internal static class SubscriptionPurchasePreviewRequests
{
    public const string PremiumRoute =
        "/account/billing/subscription/purchase/preview?additionalStorage=0&country=US&postalCode=12345";

    public const string OrganizationRoute = "/organizations/billing/subscription/purchase/preview";

    public static object OrganizationBody() => new
    {
        purchase = new
        {
            tier = "Families",
            cadence = "Annually",
            passwordManager = new { seats = 1, additionalStorage = 0, sponsored = false }
        },
        billingAddress = new { country = "US", postalCode = "12345" }
    };
}

/// <summary>The purchase preview routes require the caller to be signed in.</summary>
public class SubscriptionPurchasePreviewAuthTests : IClassFixture<ApiApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public SubscriptionPurchasePreviewAuthTests(ApiApplicationFactory factory) =>
        _client = factory.CreateClient();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task PremiumPurchasePreview_WithoutABearerToken_Returns401()
    {
        var response = await _client.GetAsync(SubscriptionPurchasePreviewRequests.PremiumRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationPurchasePreview_WithoutABearerToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            SubscriptionPurchasePreviewRequests.OrganizationRoute,
            SubscriptionPurchasePreviewRequests.OrganizationBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
