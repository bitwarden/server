using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class ProviderSeederTests
{
    private const string Name = "Acme MSP";
    private const string Domain = "acme-msp.test";

    private static ProviderSeed Seed() => new() { Name = Name, Domain = Domain };

    [Theory]
    [InlineData(ProviderType.Msp)]
    [InlineData(ProviderType.BusinessUnit)]
    public void Create_SetsBillableStatusEnabledAndStripeGateway(ProviderType type)
    {
        var provider = ProviderSeeder.Create(Seed() with { Type = type }, new NoOpManglerService());

        Assert.NotEqual(default, provider.Id);
        Assert.Equal(type, provider.Type);
        Assert.Equal(ProviderStatusType.Billable, provider.Status);
        Assert.True(provider.Enabled);
        Assert.False(provider.UseEvents);
        Assert.Equal(GatewayType.Stripe, provider.Gateway);
    }

    [Fact]
    public void Create_DerivesNonDeliverableBillingEmail()
    {
        var provider = ProviderSeeder.Create(Seed(), new NoOpManglerService());

        Assert.Equal(BillingEmailSeeder.DeriveBillingEmail(Domain), provider.BillingEmail);
        Assert.StartsWith("billing", provider.BillingEmail);
        // Non-deliverable: the domain is nested under a derived hash subdomain, never the bare domain.
        Assert.EndsWith($".{Domain}", provider.BillingEmail);
        Assert.DoesNotContain($"@{Domain}", provider.BillingEmail);
    }

    [Fact]
    public void Create_SetsBillingGatewayIdentifiers()
    {
        var provider = ProviderSeeder.Create(
            Seed() with
            {
                Gateway = GatewayType.Stripe,
                GatewayCustomerId = "cus_test123",
                GatewaySubscriptionId = "sub_test123"
            },
            new NoOpManglerService());

        Assert.Equal(GatewayType.Stripe, provider.Gateway);
        Assert.Equal("cus_test123", provider.GatewayCustomerId);
        Assert.Equal("sub_test123", provider.GatewaySubscriptionId);
    }

    [Fact]
    public void Create_NullGateway_DefaultsToStripe()
    {
        // Provider is the one seed factory with a gateway default; folding billing into Create must
        // not wipe it when the caller supplies nothing.
        var provider = ProviderSeeder.Create(Seed(), new NoOpManglerService());

        Assert.Equal(GatewayType.Stripe, provider.Gateway);
        Assert.Null(provider.GatewayCustomerId);
        Assert.Null(provider.GatewaySubscriptionId);
    }

    [Fact]
    public void Create_SetsBusinessAndBillingFields()
    {
        var provider = ProviderSeeder.Create(
            Seed() with
            {
                BusinessName = "Acme Managed Services LLC",
                BusinessCountry = "US",
                BillingPhone = "+1-555-0100"
            },
            new NoOpManglerService());

        Assert.Equal("Acme Managed Services LLC", provider.BusinessName);
        Assert.Equal("US", provider.BusinessCountry);
        Assert.Equal("+1-555-0100", provider.BillingPhone);
    }

    [Fact]
    public void Create_HonorsStatusEnabledAndUseEventsOverrides()
    {
        var provider = ProviderSeeder.Create(
            Seed() with
            {
                Status = ProviderStatusType.Pending,
                Enabled = false,
                UseEvents = true
            },
            new NoOpManglerService());

        Assert.Equal(ProviderStatusType.Pending, provider.Status);
        Assert.False(provider.Enabled);
        Assert.True(provider.UseEvents);
    }
}
