using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Billing.Controllers;

/// <summary>
/// Integration tests for the provider client invoice CSV endpoint
/// (GET /providers/{providerId}/billing/invoices/{invoiceId}) on ProviderBillingController,
/// focusing on cross-provider authorization.
///
/// Reproduces VULN-565 (PM-36574): the action authorizes the caller against the route providerId,
/// but the report service loaded ProviderInvoiceItem rows by the attacker-supplied invoiceId without
/// checking the invoice belongs to the authorized provider. A provider admin for provider A could
/// therefore retrieve provider B's client invoice CSV by passing B's invoiceId through A's route.
///
/// The report must be scoped to the authorized provider, so the victim provider's client billing
/// data must never appear in the attacker's response.
/// </summary>
public class ProviderBillingControllerAuthorizationTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _attackerAdminEmail = null!;
    private Provider _attackerProvider = null!;
    private Provider _victimProvider = null!;
    private string _victimInvoiceId = null!;

    // Distinctive victim values that must never leak into the attacker's response.
    private const string VictimClientName = "Victim Client Organization";
    private const string VictimPlanName = "Enterprise (Annually)";

    public ProviderBillingControllerAuthorizationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        var userRepository = _factory.GetService<IUserRepository>();
        var providerRepository = _factory.GetService<IProviderRepository>();
        var providerUserRepository = _factory.GetService<IProviderUserRepository>();
        var providerInvoiceItemRepository = _factory.GetService<IProviderInvoiceItemRepository>();

        // Attacker: a provider admin of their own billable provider (provider A).
        _attackerAdminEmail = $"vuln565-attacker-{Guid.NewGuid()}@test.com";
        await _factory.LoginWithNewAccount(_attackerAdminEmail);
        var attackerAdmin = await userRepository.GetByEmailAsync(_attackerAdminEmail);

        _attackerProvider = await CreateBillableProviderAsync(providerRepository, "Attacker Provider");
        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = _attackerProvider.Id,
            UserId = attackerAdmin!.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
            Key = Guid.NewGuid().ToString()
        });

        // Victim: a different provider (provider B) that owns an invoice item. The attacker has no
        // membership in this provider.
        _victimProvider = await CreateBillableProviderAsync(providerRepository, "Victim Provider");
        _victimInvoiceId = $"in_{Guid.NewGuid():N}";
        await providerInvoiceItemRepository.CreateAsync(new ProviderInvoiceItem
        {
            ProviderId = _victimProvider.Id,
            InvoiceId = _victimInvoiceId,
            InvoiceNumber = "INV-VICTIM-001",
            ClientId = Guid.NewGuid(),
            ClientName = VictimClientName,
            PlanName = VictimPlanName,
            AssignedSeats = 42,
            UsedSeats = 17,
            Total = 1234.56m
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Control: the attacker cannot use the victim provider's own route — they are not a provider
    /// admin of provider B, so authorization rejects the request. Passes before and after the fix;
    /// anchors the IDOR test below by confirming the attacker has no legitimate access to provider B.
    /// </summary>
    [Fact]
    public async Task GenerateClientInvoiceReport_ThroughVictimProviderRoute_IsUnauthorized()
    {
        await _loginHelper.LoginAsync(_attackerAdminEmail);

        var response = await _client.GetAsync(
            $"providers/{_victimProvider.Id}/billing/invoices/{_victimInvoiceId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Reproduces VULN-565: the attacker requests the victim's invoiceId through their OWN provider's
    /// route, which passes authorization. The report must be scoped to the authorized provider, so
    /// none of the victim provider's client billing data may be returned.
    /// </summary>
    [Fact]
    public async Task GenerateClientInvoiceReport_ForInvoiceOwnedByAnotherProvider_DoesNotReturnVictimData()
    {
        await _loginHelper.LoginAsync(_attackerAdminEmail);

        var response = await _client.GetAsync(
            $"providers/{_attackerProvider.Id}/billing/invoices/{_victimInvoiceId}");

        var body = await response.Content.ReadAsStringAsync();

        // Core security invariant: the authorized provider does not own this invoice, so the victim
        // provider's client billing data must not appear anywhere in the response.
        Assert.DoesNotContain(VictimClientName, body);
        Assert.DoesNotContain(VictimPlanName, body);

        // The endpoint must not hand back a CSV for an invoice the provider doesn't own; the
        // authorized provider has no such invoice, so the scoped lookup yields nothing -> 404.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<Provider> CreateBillableProviderAsync(IProviderRepository providerRepository, string name) =>
        providerRepository.CreateAsync(new Provider
        {
            Name = name,
            BillingEmail = $"{name.Replace(" ", "-").ToLowerInvariant()}@example.com",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Billable,
            Enabled = true,
            GatewayCustomerId = $"cus_{Guid.NewGuid():N}",
            GatewaySubscriptionId = $"sub_{Guid.NewGuid():N}"
        });
}
