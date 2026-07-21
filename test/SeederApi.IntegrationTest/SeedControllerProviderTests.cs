using System.Text.Json;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

/// <summary>
/// Exercises the composable SingleProviderScene end-to-end through POST /seed, matching the Provider-Scenes
/// postman collection: seed a provider-admin user, a SEPARATE org-owner user, a client organization owned by
/// the org owner, then a provider that links the admin (as a confirmed ProviderAdmin) and the client org,
/// then cleanup via DELETE /seed/{playId} (providers are torn down first).
/// </summary>
public class SeedControllerProviderTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public SeedControllerProviderTests(InPlaySeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_factory.Username, _factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync("/seed");
        _client.Dispose();
    }

    [Fact]
    public async Task SingleProviderScene_LinksExistingOwnerAndClientOrg_ThenCleansUp()
    {
        var playId = Guid.NewGuid().ToString();

        var providerAdminUserId = await SeedUserAsync(playId);
        var orgOwnerUserId = await SeedUserAsync(playId);
        Assert.NotEqual(providerAdminUserId, orgOwnerUserId);

        var (organizationId, organizationKeyB64) = await SeedClientOrganizationAsync(playId, orgOwnerUserId);

        var providerResult = await PostSceneAsync(playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = providerAdminUserId,
            Name = "Acme MSP",
            Type = ProviderType.Msp,
            Plans =
            [
                new SingleProviderScene.PlanConfig { PlanType = PlanType.TeamsMonthly, Seats = 5 },
                new SingleProviderScene.PlanConfig { PlanType = PlanType.EnterpriseMonthly, Seats = 10 }
            ],
            Organizations =
            [
                new SingleProviderScene.OrganizationLink
                {
                    OrganizationId = organizationId,
                    OrganizationKeyB64 = organizationKeyB64
                }
            ],
            GatewayCustomerId = "cus_test123",
            GatewaySubscriptionId = "sub_test123"
        });

        var providerId = providerResult.GetProperty("providerId").GetGuid();
        Assert.False(string.IsNullOrEmpty(providerResult.GetProperty("providerKeyB64").GetString()));
        Assert.Single(providerResult.GetProperty("providerOrganizationIds").EnumerateArray());
        Assert.NotEqual(Guid.Empty, providerResult.GetProperty("providerUserId").GetGuid());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var provider = await db.Providers.SingleAsync(p => p.Id == providerId);
            Assert.Equal(ProviderType.Msp, provider.Type);
            Assert.Equal(ProviderStatusType.Billable, provider.Status);
            Assert.True(provider.Enabled);
            Assert.Equal(GatewayType.Stripe, provider.Gateway);
            Assert.Equal("cus_test123", provider.GatewayCustomerId);
            Assert.Equal("sub_test123", provider.GatewaySubscriptionId);

            var admin = await db.ProviderUsers.SingleAsync(pu =>
                pu.ProviderId == providerId && pu.Type == ProviderUserType.ProviderAdmin);
            Assert.Equal(providerAdminUserId, admin.UserId);
            Assert.Equal(ProviderUserStatusType.Confirmed, admin.Status);
            Assert.False(string.IsNullOrEmpty(admin.Key));

            var orgLink = await db.ProviderOrganizations.SingleAsync(po => po.ProviderId == providerId);
            Assert.Equal(organizationId, orgLink.OrganizationId);
            // The organization key is wrapped under the provider key as a type-2 EncString (not the base64
            // text), so a client can unwrap it back into the organization key via unwrap_symmetric_key.
            Assert.StartsWith("2.", orgLink.Key);

            var plans = await db.ProviderPlans.Where(pp => pp.ProviderId == providerId).ToListAsync();
            Assert.Equal(2, plans.Count);
            Assert.Contains(plans, pp => pp.PlanType == PlanType.TeamsMonthly);
            Assert.Contains(plans, pp => pp.PlanType == PlanType.EnterpriseMonthly);
            Assert.All(plans, pp => Assert.True(pp.IsConfigured()));
        }

        var deleteResponse = await _client.DeleteAsync($"/seed/{playId}");
        deleteResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            // Providers (and their cascading users/orgs/plans links) are deleted first, then the linked
            // owner/org-owner users and the client organization tracked under the same play.
            Assert.False(await db.Providers.AnyAsync(p => p.Id == providerId));
            Assert.False(await db.ProviderUsers.AnyAsync(pu => pu.ProviderId == providerId));
            Assert.False(await db.ProviderOrganizations.AnyAsync(po => po.ProviderId == providerId));
            Assert.False(await db.ProviderPlans.AnyAsync(pp => pp.ProviderId == providerId));

            Assert.False(await db.Users.AnyAsync(u => u.Id == providerAdminUserId || u.Id == orgOwnerUserId));
            Assert.False(await db.Organizations.AnyAsync(o => o.Id == organizationId));
        }
    }

    private async Task<Guid> SeedUserAsync(string playId)
    {
        var result = await PostSceneAsync(playId, "SingleUserScene", new SingleUserScene.Request
        {
            Email = $"user-{Guid.NewGuid():N}@example.com",
            Password = "asdfasdfasdf",
            EmailVerified = true
        });

        return result.GetProperty("userId").GetGuid();
    }

    private async Task<(Guid OrganizationId, string OrganizationKeyB64)> SeedClientOrganizationAsync(
        string playId, Guid ownerUserId)
    {
        var result = await PostSceneAsync(playId, "SingleOrganizationScene", new SingleOrganizationScene.Request
        {
            OwnerUserId = ownerUserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "Provider Client Org",
            Domain = $"client-{Guid.NewGuid():N}.example.com",
            Seats = 10
        });

        return (result.GetProperty("organizationId").GetGuid(),
            result.GetProperty("organizationKeyB64").GetString()!);
    }

    private async Task<JsonElement> PostSceneAsync<TRequest>(string playId, string template, TRequest arguments)
    {
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = template,
            Arguments = JsonSerializer.SerializeToElement(arguments)
        }, playId);

        response.EnsureSuccessStatusCode();

        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(model);
        Assert.NotNull(model!.Result);
        return (JsonElement)model.Result!;
    }
}
