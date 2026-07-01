using Bit.Core.Billing.Pricing;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Pricing;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPricingClient_WhenPricingUriIsSet_ResolvesHttpPricingClient()
    {
        var services = BuildServices(pricingUri: "https://pricing.example.test/", environmentName: Environments.Production);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPricingClient>();

        Assert.IsType<HttpPricingClient>(client);
    }

    [Fact]
    public void AddPricingClient_WhenPricingUriIsEmptyAndDevelopment_ResolvesLocalPricingClient()
    {
        var services = BuildServices(pricingUri: null, environmentName: Environments.Development);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPricingClient>();

        Assert.IsNotType<HttpPricingClient>(client);
        Assert.Equal("LocalPricingClient", client.GetType().Name);
    }

    [Fact]
    public void AddPricingClient_WhenPricingUriIsEmptyAndNotDevelopment_ThrowsOnResolve()
    {
        var services = BuildServices(pricingUri: "", environmentName: Environments.Production);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IPricingClient>());
    }

    [Fact]
    public void AddPricingClient_WhenIPricingClientAlreadyRegistered_PreservesExistingRegistration()
    {
        var services = BuildServices(pricingUri: "https://pricing.example.test/", environmentName: Environments.Production);
        var preregistered = Substitute.For<IPricingClient>();
        services.AddSingleton(preregistered);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPricingClient>();

        Assert.Same(preregistered, client);
    }

    [Fact]
    public void AddPricingClient_WhenResolvingLocalClient_ReturnsSameInstanceAcrossResolutions()
    {
        var services = BuildServices(pricingUri: null, environmentName: Environments.Development);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IPricingClient>();
        var second = provider.GetRequiredService<IPricingClient>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddPricingClient_WhenResolvingNoopClient_ReturnsSameInstanceAcrossResolutions()
    {
        var services = BuildServices(
            pricingUri: "https://pricing.example.test/",
            environmentName: Environments.Production,
            selfHosted: true);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IPricingClient>();
        var second = provider.GetRequiredService<IPricingClient>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddPricingClient_WhenResolvingHttpClient_ReturnsFreshInstancePerResolution()
    {
        var services = BuildServices(pricingUri: "https://pricing.example.test/", environmentName: Environments.Production);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IPricingClient>();
        var second = provider.GetRequiredService<IPricingClient>();

        // HttpPricingClient is registered via AddHttpClient (transient typed client) so each resolution
        // gets a fresh instance backed by an HttpClient with rotated handlers.
        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddPricingClient_WhenSelfHosted_ResolvesNoopClientRegardlessOfPricingUri()
    {
        var services = BuildServices(
            pricingUri: "https://pricing.example.test/",
            environmentName: Environments.Production,
            selfHosted: true);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPricingClient>();

        Assert.IsNotType<HttpPricingClient>(client);
        Assert.Equal("NoopPricingClient", client.GetType().Name);
    }

    [Fact]
    public void AddPricingClient_WhenSelfHostedAndPricingUriMissing_DoesNotThrowOnResolve()
    {
        var services = BuildServices(
            pricingUri: null,
            environmentName: Environments.Production,
            selfHosted: true);

        services.AddPricingClient();

        using var provider = services.BuildServiceProvider();

        // Pre-split behavior: self-host with no pricing service must resolve cleanly, not fail-fast.
        var client = provider.GetRequiredService<IPricingClient>();
        Assert.NotNull(client);
    }

    private static ServiceCollection BuildServices(
        string? pricingUri,
        string environmentName,
        bool selfHosted = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var globalSettings = new GlobalSettings
        {
            PricingUri = pricingUri,
            SelfHosted = selfHosted,
        };
        services.AddSingleton(globalSettings);

        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(environmentName);
        services.AddSingleton(hostEnvironment);

        return services;
    }
}
