using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class ProviderSeederTests
{
    private const string Name = "Acme MSP";
    private const string Domain = "acme-msp.test";

    [Theory]
    [InlineData(ProviderType.Msp)]
    [InlineData(ProviderType.BusinessUnit)]
    public void Create_SetsBillableStatusEnabledAndStripeGateway(ProviderType type)
    {
        var provider = ProviderSeeder.Create(Name, Domain, type, new NoOpManglerService());

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
        var provider = ProviderSeeder.Create(Name, Domain, ProviderType.Msp, new NoOpManglerService());

        Assert.Equal(SeederBilling.DeriveBillingEmail(Domain), provider.BillingEmail);
        Assert.StartsWith("billing", provider.BillingEmail);
        // Non-deliverable: the domain is nested under a derived hash subdomain, never the bare domain.
        Assert.EndsWith($".{Domain}", provider.BillingEmail);
        Assert.DoesNotContain($"@{Domain}", provider.BillingEmail);
    }

    [Fact]
    public void ApplyBilling_SetsGatewayIdentifiers()
    {
        var provider = ProviderSeeder.Create(Name, Domain, ProviderType.Msp, new NoOpManglerService());

        ProviderSeeder.ApplyBilling(provider, GatewayType.Stripe, "cus_test123", "sub_test123");

        Assert.Equal(GatewayType.Stripe, provider.Gateway);
        Assert.Equal("cus_test123", provider.GatewayCustomerId);
        Assert.Equal("sub_test123", provider.GatewaySubscriptionId);
    }

    [Fact]
    public void ApplyBilling_NullValues_LeaveFieldsUnchanged()
    {
        var provider = ProviderSeeder.Create(Name, Domain, ProviderType.Msp, new NoOpManglerService());

        ProviderSeeder.ApplyBilling(provider, null, null, null);

        Assert.Equal(GatewayType.Stripe, provider.Gateway);
        Assert.Null(provider.GatewayCustomerId);
        Assert.Null(provider.GatewaySubscriptionId);
    }
}
