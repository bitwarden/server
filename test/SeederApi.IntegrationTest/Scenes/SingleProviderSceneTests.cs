using System.Net;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Options;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

/// <summary>
/// Exercises the composable SingleProviderScene end-to-end through POST /seed, matching the Provider-Scenes
/// postman collection: seed a provider-admin user, a SEPARATE org-owner user, a client organization owned by
/// the org owner, then a provider that links the admin (as a confirmed ProviderAdmin) and the client org,
/// then cleanup via DELETE /seed/{playId} (providers are torn down first).
/// </summary>
public class SingleProviderSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public SingleProviderSceneTests(InPlaySeederApiApplicationFactory factory)
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

    [Theory]
    [InlineData(ProviderType.Msp, ProviderStatusType.Pending)]
    [InlineData(ProviderType.BusinessUnit, ProviderStatusType.Pending)]
    [InlineData(ProviderType.Reseller, ProviderStatusType.Created)]
    public async Task SingleProviderScene_PassesProviderStatusThrough(ProviderType type, ProviderStatusType status)
    {
        var playId = Guid.NewGuid().ToString();

        var providerAdminUserId = await SeedUserAsync(playId);

        var providerResult = await PostSceneAsync(playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = providerAdminUserId,
            Name = $"Status {type}",
            Type = type,
            Status = status
        });
        var providerId = providerResult.GetProperty("providerId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var provider = await db.Providers.SingleAsync(p => p.Id == providerId);
        Assert.Equal(type, provider.Type);
        Assert.Equal(status, provider.Status);
    }

    [Theory]
    [InlineData(ProviderType.Msp, ProviderStatusType.Created)]
    [InlineData(ProviderType.BusinessUnit, ProviderStatusType.Created)]
    [InlineData(ProviderType.Reseller, ProviderStatusType.Billable)]
    [InlineData(ProviderType.Reseller, ProviderStatusType.Pending)]
    public async Task SingleProviderScene_InvalidTypeStatusPair_ReturnsBadRequestWithoutWrites(
        ProviderType type, ProviderStatusType status)
    {
        var playId = Guid.NewGuid().ToString();
        var providerName = $"Invalid {type} {status} {Guid.NewGuid():N}";

        var providerAdminUserId = await SeedUserAsync(playId);
        var orgOwnerUserId = await SeedUserAsync(playId);
        var (organizationId, organizationKeyB64) = await SeedClientOrganizationAsync(playId, orgOwnerUserId);

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(SingleProviderScene),
            Arguments = JsonSerializer.SerializeToElement(new SingleProviderScene.Request
            {
                OwnerUserId = providerAdminUserId,
                Name = providerName,
                Type = type,
                Status = status,
                Plans = [new SingleProviderScene.PlanConfig { PlanType = PlanType.EnterpriseMonthly, Seats = 5 }],
                Organizations = [Link(organizationId, organizationKeyB64)]
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        Assert.False(await db.Providers.AnyAsync(p => p.Name!.EndsWith(providerName)));
        Assert.False(await db.ProviderUsers.AnyAsync(pu => pu.UserId == providerAdminUserId));
        Assert.False(await db.ProviderOrganizations.AnyAsync(po => po.OrganizationId == organizationId));
        var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId);
        Assert.Equal(OrganizationStatusType.Created, organization.Status);
        Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
    }

    [Theory]
    [InlineData("cus_pending", null)]
    [InlineData(null, "sub_pending")]
    public async Task SingleProviderScene_PendingProviderWithGatewayIds_ReturnsBadRequestWithoutWrites(
        string? gatewayCustomerId, string? gatewaySubscriptionId)
    {
        var playId = Guid.NewGuid().ToString();
        var providerName = $"Pending Gateway {Guid.NewGuid():N}";

        var providerAdminUserId = await SeedUserAsync(playId);

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(SingleProviderScene),
            Arguments = JsonSerializer.SerializeToElement(new SingleProviderScene.Request
            {
                OwnerUserId = providerAdminUserId,
                Name = providerName,
                Type = ProviderType.Msp,
                Status = ProviderStatusType.Pending,
                GatewayCustomerId = gatewayCustomerId,
                GatewaySubscriptionId = gatewaySubscriptionId
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        Assert.False(await db.Providers.AnyAsync(p => p.Name!.EndsWith(providerName)));
        Assert.False(await db.ProviderUsers.AnyAsync(pu => pu.UserId == providerAdminUserId));
    }

    [Theory]
    [InlineData(ProviderType.Msp)]
    [InlineData(ProviderType.BusinessUnit)]
    public async Task SingleProviderScene_PendingProviderLinksOrgWithoutConverting(ProviderType type)
    {
        var playId = Guid.NewGuid().ToString();

        var providerAdminUserId = await SeedUserAsync(playId);
        var orgOwnerUserId = await SeedUserAsync(playId);
        var (organizationId, organizationKeyB64) = await SeedClientOrganizationAsync(
            playId,
            orgOwnerUserId,
            gatewayCustomerId: "cus_seed",
            gatewaySubscriptionId: "sub_seed");

        var providerResult = await PostSceneAsync(playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = providerAdminUserId,
            Name = $"Pending {type}",
            Type = type,
            Status = ProviderStatusType.Pending,
            Plans = [new SingleProviderScene.PlanConfig { PlanType = PlanType.EnterpriseMonthly, Seats = 5 }],
            Organizations = [Link(organizationId, organizationKeyB64)]
        });
        var providerId = providerResult.GetProperty("providerId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        Assert.True(await db.ProviderOrganizations.AnyAsync(po =>
            po.ProviderId == providerId && po.OrganizationId == organizationId));

        var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId);
        Assert.Equal(OrganizationStatusType.Created, organization.Status);
        Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
        Assert.Equal("sub_seed", organization.GatewaySubscriptionId);

        var plan = await db.ProviderPlans.SingleAsync(pp => pp.ProviderId == providerId);
        Assert.Equal(0, plan.AllocatedSeats);
    }

    [Fact]
    public async Task SingleProviderScene_ConvertsLinkedOrgsToManagedClients()
    {
        var playId = Guid.NewGuid().ToString();

        var providerAdminUserId = await SeedUserAsync(playId);
        var orgOwnerUserId = await SeedUserAsync(playId);
        var (organizationId, organizationKeyB64) = await SeedClientOrganizationAsync(
            playId,
            orgOwnerUserId,
            gatewayCustomerId: "cus_seed",
            gatewaySubscriptionId: "sub_seed",
            overrides: new OrganizationOverrides { SyncSeats = false },
            enableSecretsManager: true,
            smSeats: 4);

        var providerResult = await PostSceneAsync(playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = providerAdminUserId,
            Name = "Managed MSP",
            Type = ProviderType.Msp,
            Plans =
            [
                new SingleProviderScene.PlanConfig { PlanType = PlanType.TeamsMonthly, Seats = 5 },
                new SingleProviderScene.PlanConfig { PlanType = PlanType.EnterpriseMonthly, Seats = 10 }
            ],
            Organizations = [Link(organizationId, organizationKeyB64)]
        });
        var providerId = providerResult.GetProperty("providerId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var provider = await db.Providers.SingleAsync(p => p.Id == providerId);
        var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId);
        Assert.Equal(OrganizationStatusType.Managed, organization.Status);
        Assert.Equal(PlanType.EnterpriseMonthly, organization.PlanType);
        Assert.Equal("Enterprise (Monthly)", organization.Plan);
        Assert.False(organization.SyncSeats);
        Assert.True(organization.UseSecretsManager);
        Assert.Equal(4, organization.SmSeats);
        Assert.Equal(provider.BillingEmail, organization.BillingEmail);
        Assert.Equal("cus_seed", organization.GatewayCustomerId);
        Assert.Null(organization.GatewaySubscriptionId);
    }

    [Fact]
    public async Task SingleProviderScene_BusinessUnitConvertsTeamsOrgToEnterpriseClient()
    {
        var playId = Guid.NewGuid().ToString();

        var providerAdminUserId = await SeedUserAsync(playId);
        var orgOwnerUserId = await SeedUserAsync(playId);
        var (organizationId, organizationKeyB64) =
            await SeedClientOrganizationAsync(playId, orgOwnerUserId, PlanType.TeamsAnnually, 6);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var seeded = await db.Organizations.SingleAsync(o => o.Id == organizationId);
            Assert.False(seeded.UseSso);
            Assert.False(seeded.UsePolicies);
            Assert.False(seeded.UseResetPassword);
            Assert.False(seeded.UseMyItems);
        }

        var providerResult = await PostSceneAsync(playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = providerAdminUserId,
            Name = "Managed Business Unit",
            Type = ProviderType.BusinessUnit,
            Plans = [new SingleProviderScene.PlanConfig { PlanType = PlanType.EnterpriseAnnually, Seats = 2 }],
            Organizations = [Link(organizationId, organizationKeyB64)]
        });
        var providerId = providerResult.GetProperty("providerId").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId);
            Assert.Equal(OrganizationStatusType.Managed, organization.Status);
            Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
            Assert.Equal("Enterprise (Annually)", organization.Plan);
            Assert.True(organization.UseSso);
            Assert.True(organization.UsePolicies);
            Assert.True(organization.UseResetPassword);
            Assert.True(organization.UseMyItems);

            var plan = await db.ProviderPlans.SingleAsync(pp => pp.ProviderId == providerId);
            Assert.Equal(PlanType.EnterpriseAnnually, plan.PlanType);
            Assert.Equal(6, plan.AllocatedSeats);
        }
    }

    [Fact]
    public async Task SingleProviderScene_AllocatesSeatsPerPlanFromLinkedClients()
    {
        var playId = Guid.NewGuid().ToString();

        var providerAdminUserId = await SeedUserAsync(playId);
        var orgOwnerUserId = await SeedUserAsync(playId);
        var teamsAnnual = await SeedClientOrganizationAsync(playId, orgOwnerUserId, PlanType.TeamsAnnually, 3);
        var enterpriseAnnual = await SeedClientOrganizationAsync(playId, orgOwnerUserId, PlanType.EnterpriseAnnually, 7);
        var enterpriseMonthly = await SeedClientOrganizationAsync(playId, orgOwnerUserId, PlanType.EnterpriseMonthly, 4);

        var providerResult = await PostSceneAsync(playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = providerAdminUserId,
            Name = "Seat MSP",
            Type = ProviderType.Msp,
            Plans =
            [
                new SingleProviderScene.PlanConfig { PlanType = PlanType.TeamsMonthly, Seats = 20 },
                new SingleProviderScene.PlanConfig { PlanType = PlanType.EnterpriseMonthly, Seats = 5 }
            ],
            Organizations =
            [
                Link(teamsAnnual.OrganizationId, teamsAnnual.OrganizationKeyB64),
                Link(enterpriseAnnual.OrganizationId, enterpriseAnnual.OrganizationKeyB64),
                Link(enterpriseMonthly.OrganizationId, enterpriseMonthly.OrganizationKeyB64)
            ]
        });
        var providerId = providerResult.GetProperty("providerId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var plans = await db.ProviderPlans.Where(pp => pp.ProviderId == providerId).ToListAsync();

        var teams = plans.Single(pp => pp.PlanType == PlanType.TeamsMonthly);
        Assert.Equal(20, teams.SeatMinimum);
        Assert.Equal(3, teams.AllocatedSeats);
        Assert.Equal(0, teams.PurchasedSeats);

        var enterprise = plans.Single(pp => pp.PlanType == PlanType.EnterpriseMonthly);
        Assert.Equal(5, enterprise.SeatMinimum);
        Assert.Equal(11, enterprise.AllocatedSeats);
        Assert.Equal(6, enterprise.PurchasedSeats);
    }

    [Theory]
    [InlineData(ProviderType.Msp)]
    [InlineData(ProviderType.BusinessUnit)]
    public async Task SingleProviderScene_BillableProviderWithoutOrganizations_SeedsUnallocatedPlan(ProviderType type)
    {
        var playId = Guid.NewGuid().ToString();

        var providerAdminUserId = await SeedUserAsync(playId);

        var providerResult = await PostSceneAsync(playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = providerAdminUserId,
            Name = $"Empty {type}",
            Type = type,
            Plans = [new SingleProviderScene.PlanConfig { PlanType = PlanType.EnterpriseMonthly, Seats = 10 }]
        });
        var providerId = providerResult.GetProperty("providerId").GetGuid();
        Assert.Empty(providerResult.GetProperty("providerOrganizationIds").EnumerateArray());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var provider = await db.Providers.SingleAsync(p => p.Id == providerId);
        Assert.Equal(ProviderStatusType.Billable, provider.Status);
        Assert.False(await db.ProviderOrganizations.AnyAsync(po => po.ProviderId == providerId));

        var plan = await db.ProviderPlans.SingleAsync(pp => pp.ProviderId == providerId);
        Assert.Equal(PlanType.EnterpriseMonthly, plan.PlanType);
        Assert.Equal(10, plan.SeatMinimum);
        Assert.Equal(0, plan.AllocatedSeats);
        Assert.Equal(0, plan.PurchasedSeats);
    }

    [Theory]
    [InlineData(ProviderType.Msp)]
    [InlineData(ProviderType.BusinessUnit)]
    public async Task SingleProviderScene_LinkedOrgWithoutMatchingPlan_ReturnsBadRequestWithoutWrites(ProviderType type)
    {
        var playId = Guid.NewGuid().ToString();
        var providerName = $"Invalid {type} {Guid.NewGuid():N}";

        var providerAdminUserId = await SeedUserAsync(playId);
        var orgOwnerUserId = await SeedUserAsync(playId);
        var (organizationId, organizationKeyB64) = await SeedClientOrganizationAsync(playId, orgOwnerUserId);

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(SingleProviderScene),
            Arguments = JsonSerializer.SerializeToElement(new SingleProviderScene.Request
            {
                OwnerUserId = providerAdminUserId,
                Name = providerName,
                Type = type,
                Plans = [new SingleProviderScene.PlanConfig { PlanType = PlanType.TeamsMonthly, Seats = 5 }],
                Organizations = [Link(organizationId, organizationKeyB64)]
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        Assert.False(await db.Providers.AnyAsync(p => p.Name!.EndsWith(providerName)));
        Assert.False(await db.ProviderUsers.AnyAsync(pu => pu.UserId == providerAdminUserId));
        Assert.False(await db.ProviderOrganizations.AnyAsync(po => po.OrganizationId == organizationId));
        var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId);
        Assert.Equal(OrganizationStatusType.Created, organization.Status);
        Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
    }

    [Fact]
    public async Task SingleProviderScene_MalformedOrganizationKey_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var providerName = $"Bad Key {Guid.NewGuid():N}";

        var providerAdminUserId = await SeedUserAsync(playId);
        var orgOwnerUserId = await SeedUserAsync(playId);
        var valid = await SeedClientOrganizationAsync(playId, orgOwnerUserId);
        var malformed = await SeedClientOrganizationAsync(playId, orgOwnerUserId);

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(SingleProviderScene),
            Arguments = JsonSerializer.SerializeToElement(new SingleProviderScene.Request
            {
                OwnerUserId = providerAdminUserId,
                Name = providerName,
                Type = ProviderType.Msp,
                Plans = [new SingleProviderScene.PlanConfig { PlanType = PlanType.EnterpriseMonthly, Seats = 5 }],
                Organizations =
                [
                    Link(valid.OrganizationId, valid.OrganizationKeyB64),
                    Link(malformed.OrganizationId, "not-a-key")
                ]
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        Assert.False(await db.Providers.AnyAsync(p => p.Name!.EndsWith(providerName)));
        Assert.False(await db.ProviderUsers.AnyAsync(pu => pu.UserId == providerAdminUserId));
        Assert.False(await db.ProviderOrganizations.AnyAsync(po =>
            po.OrganizationId == valid.OrganizationId || po.OrganizationId == malformed.OrganizationId));
        var organizations = await db.Organizations
            .Where(o => o.Id == valid.OrganizationId || o.Id == malformed.OrganizationId)
            .ToListAsync();
        Assert.Equal(2, organizations.Count);
        Assert.All(organizations, o =>
        {
            Assert.Equal(OrganizationStatusType.Created, o.Status);
            Assert.Equal(PlanType.EnterpriseAnnually, o.PlanType);
        });
    }

    [Theory]
    [InlineData(ProviderUserType.ServiceUser, ProviderUserStatusType.Confirmed)]
    [InlineData(ProviderUserType.ProviderAdmin, ProviderUserStatusType.Accepted)]
    [InlineData(ProviderUserType.ServiceUser, ProviderUserStatusType.Invited)]
    public async Task ProviderUserScene_CreatesMemberWithTypeAndStatus(
        ProviderUserType type, ProviderUserStatusType status)
    {
        var playId = Guid.NewGuid().ToString();

        var providerAdminUserId = await SeedUserAsync(playId);
        var memberUserId = await SeedUserAsync(playId);

        var providerResult = await PostSceneAsync(playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = providerAdminUserId,
            Name = "Member MSP",
            Type = ProviderType.Msp
        });
        var providerId = providerResult.GetProperty("providerId").GetGuid();

        var memberResult = await PostSceneAsync(playId, nameof(ProviderUserScene), new ProviderUserScene.Request
        {
            UserId = memberUserId,
            ProviderId = providerId,
            ProviderKeyB64 = providerResult.GetProperty("providerKeyB64").GetString()!,
            ProviderUserType = type,
            ProviderUserStatusType = status
        });
        var providerUserId = memberResult.GetProperty("providerUserId").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var member = await db.ProviderUsers.SingleAsync(pu => pu.Id == providerUserId);
            Assert.Equal(providerId, member.ProviderId);
            Assert.Equal(type, member.Type);
            Assert.Equal(status, member.Status);

            if (status == ProviderUserStatusType.Invited)
            {
                Assert.Null(member.UserId);
                Assert.False(string.IsNullOrEmpty(member.Email));
            }
            else
            {
                Assert.Equal(memberUserId, member.UserId);
            }

            if (status == ProviderUserStatusType.Confirmed)
            {
                Assert.StartsWith("4.", member.Key);
            }
            else
            {
                Assert.Null(member.Key);
            }
        }

        var deleteResponse = await _client.DeleteAsync($"/seed/{playId}");
        deleteResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            Assert.False(await db.ProviderUsers.AnyAsync(pu => pu.Id == providerUserId));
        }
    }

    private static SingleProviderScene.OrganizationLink Link(Guid organizationId, string organizationKeyB64) =>
        new() { OrganizationId = organizationId, OrganizationKeyB64 = organizationKeyB64 };

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
        string playId,
        Guid ownerUserId,
        PlanType planType = PlanType.EnterpriseAnnually,
        int seats = 10,
        string? gatewayCustomerId = null,
        string? gatewaySubscriptionId = null,
        OrganizationOverrides? overrides = null,
        bool enableSecretsManager = false,
        int? smSeats = null)
    {
        var result = await PostSceneAsync(playId, "SingleOrganizationScene", new SingleOrganizationScene.Request
        {
            OwnerUserId = ownerUserId,
            PlanType = planType,
            Name = "Provider Client Org",
            Domain = $"client-{Guid.NewGuid():N}.example.com",
            Seats = seats,
            GatewayCustomerId = gatewayCustomerId,
            GatewaySubscriptionId = gatewaySubscriptionId,
            Overrides = overrides,
            EnableSecretsManager = enableSecretsManager,
            SmSeats = smSeats
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
