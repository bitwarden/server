using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationScopedApiKeysControllerFeatureFlagOffTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    private Organization _organization = null!;

    public OrganizationScopedApiKeysControllerFeatureFlagOffTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        await new LoginHelper(_factory, _client).LoginAsync(ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AllEndpoints_FeatureFlagOff_ReturnsNotFound()
    {
        var list = await _client.GetAsync($"organizations/{_organization.Id}/scoped-api-keys");
        var create = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/scoped-api-keys", new
        {
            masterPasswordHash = "master_password_hash",
            name = "SIEM export",
            scopes = new[] { ApiScopes.ApiOrganizationEventsRead },
        });
        var revoke = await _client.DeleteAsync($"organizations/{_organization.Id}/scoped-api-keys/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }
}
