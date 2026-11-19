using Bit.Core.Billing.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class ProviderPlanSeederTests
{
    [Theory]
    [InlineData(PlanType.TeamsMonthly, 10, 7, 0)]
    [InlineData(PlanType.EnterpriseMonthly, 10, 14, 4)]
    public void Create_ProducesConfiguredProviderPlan(
        PlanType planType, int seatMinimum, int allocatedSeats, int expectedPurchasedSeats)
    {
        var provider = ProviderSeeder.Create(
            new ProviderSeed { Name = "Acme MSP", Domain = "acme-msp.test" },
            new NoOpManglerService());

        var providerPlan = ProviderPlanSeeder.Create(provider, planType, seatMinimum, allocatedSeats);

        Assert.NotEqual(default, providerPlan.Id);
        Assert.Equal(provider.Id, providerPlan.ProviderId);
        Assert.Equal(planType, providerPlan.PlanType);
        Assert.Equal(seatMinimum, providerPlan.SeatMinimum);
        Assert.Equal(expectedPurchasedSeats, providerPlan.PurchasedSeats);
        Assert.Equal(allocatedSeats, providerPlan.AllocatedSeats);
        Assert.True(providerPlan.IsConfigured());
    }
}
