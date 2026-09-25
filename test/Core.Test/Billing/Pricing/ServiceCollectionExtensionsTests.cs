using Bit.Core.Billing.Pricing;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Core.Test.Billing.Pricing;

public class ServiceCollectionExtensionsTests
{
    private const string PricingApiKeyHeader = "X-Pricing-Api-Key";

    // Obviously-synthetic test value; the server does not enforce the pricing
    // service's >=32 character rule, so any non-empty value exercises the guard.
    private const string ApiKey = "test-pricing-api-key-0123456789abcdef";

    [Fact]
    public void AddPricingClient_WhenApiKeyConfigured_SendsApiKeyHeader()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new GlobalSettings
        {
            PricingUri = "https://test.com/",
            PricingApiKey = ApiKey
        });
        services.AddLogging();
        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();

        // Act
        var httpClient = provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IPricingClient));

        // Assert
        Assert.True(httpClient.DefaultRequestHeaders.Contains(PricingApiKeyHeader));
        Assert.Equal(ApiKey, httpClient.DefaultRequestHeaders.GetValues(PricingApiKeyHeader).Single());
    }

    [Fact]
    public void AddPricingClient_WhenApiKeyNotConfigured_DoesNotSendApiKeyHeader()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new GlobalSettings { PricingUri = "https://test.com/" });
        services.AddLogging();
        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();

        // Act
        var httpClient = provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IPricingClient));

        // Assert
        Assert.False(httpClient.DefaultRequestHeaders.Contains(PricingApiKeyHeader));
    }
}
