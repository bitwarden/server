using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Invoicing;
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

/// <summary>With the preview-driven cart flag at its default (off), the purchase preview routes do not exist.</summary>
public class SubscriptionPurchasePreviewFlagOffTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;

    public SubscriptionPurchasePreviewFlagOffTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        var email = $"purchase-preview-flag-off-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await _loginHelper.LoginAsync(email);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PremiumPurchasePreview_WhenFlagIsOff_Returns404()
    {
        var response = await _client.GetAsync(SubscriptionPurchasePreviewRequests.PremiumRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationPurchasePreview_WhenFlagIsOff_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            SubscriptionPurchasePreviewRequests.OrganizationRoute,
            SubscriptionPurchasePreviewRequests.OrganizationBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>With the flag on, the purchase preview routes still require the caller to be signed in.</summary>
public class SubscriptionPurchasePreviewFlagOnTests
    : IClassFixture<SubscriptionPurchasePreviewFlagOnTests.PreviewDrivenCartApiFactory>, IDisposable
{
    private readonly HttpClient _client;

    public SubscriptionPurchasePreviewFlagOnTests(PreviewDrivenCartApiFactory factory) =>
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

    public sealed class PreviewDrivenCartApiFactory : ApiApplicationFactory
    {
        public PreviewDrivenCartApiFactory() =>
            UpdateConfiguration(
                $"globalSettings:launchDarkly:flagValues:{InvoicingFeatureFlags.PM36631_PreviewDrivenCart}",
                "true");
    }
}
