using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

/// <summary>
/// Startup validation already proves the SeederApi's Stripe billing registrations resolve, except for the real
/// <c>OrganizationBillingService</c>, which the test base substitutes unless <c>StripeEnabled</c> is set.
/// </summary>
public class StripeBillingRegistrationTests : IAsyncLifetime
{
    private readonly SeederApiApplicationFactory _factory = new() { StripeEnabled = true };

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void BillingServices_ResolveWithTheRealCoreBillingGraph()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<OrganizationStripeBillingService>(
            scope.ServiceProvider.GetRequiredService<IOrganizationStripeBillingService>());
        Assert.IsType<UserStripeBillingService>(
            scope.ServiceProvider.GetRequiredService<IUserStripeBillingService>());
    }
}
