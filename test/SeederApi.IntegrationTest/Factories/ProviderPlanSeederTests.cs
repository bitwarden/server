using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class ProviderPlanSeederTests
{
    [Theory]
    [InlineData(PlanType.TeamsMonthly)]
    [InlineData(PlanType.EnterpriseMonthly)]
    public void Create_ProducesConfiguredProviderPlan(PlanType planType)
    {
        var provider = ProviderSeeder.Create("Acme MSP", "acme-msp.test", ProviderType.Msp, new NoOpManglerService());

        var providerPlan = ProviderPlanSeeder.Create(provider, planType, 10);

        Assert.NotEqual(default, providerPlan.Id);
        Assert.Equal(provider.Id, providerPlan.ProviderId);
        Assert.Equal(planType, providerPlan.PlanType);
        Assert.Equal(10, providerPlan.SeatMinimum);
        Assert.Equal(10, providerPlan.PurchasedSeats);
        Assert.Equal(10, providerPlan.AllocatedSeats);
        Assert.True(providerPlan.IsConfigured());
    }
}
