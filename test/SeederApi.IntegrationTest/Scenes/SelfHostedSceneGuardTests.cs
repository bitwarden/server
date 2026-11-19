using System.Net;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class SelfHostedSceneGuardTests : IClassFixture<SelfHostedSeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly SelfHostedSeederApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly string _playId = Guid.NewGuid().ToString();

    public SelfHostedSceneGuardTests(SelfHostedSeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_factory.Username, _factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync($"/seed/{_playId}");
        _client.Dispose();
    }

    [Fact]
    public async Task SingleProviderScene_SelfHosted_ReturnsBadRequestWithoutWrites()
    {
        var ownerUserId = await SeedUserAsync();
        var name = $"Self-hosted MSP {Guid.NewGuid():N}";

        var response = await _client.PostSceneRequestAsync(_playId, nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = ownerUserId,
            Name = name,
            Type = ProviderType.Msp
        });

        await AssertSelfHostRejectionAsync(response, "Providers");
        Assert.False(await _factory.QueryAsync(db => db.Providers.AnyAsync(p => p.Name == name)));
        Assert.False(await _factory.QueryAsync(db => db.ProviderUsers.AnyAsync(pu => pu.UserId == ownerUserId)));
    }

    [Fact]
    public async Task ProviderUserScene_SelfHosted_ReturnsBadRequestWithoutWrites()
    {
        var userId = await SeedUserAsync();

        var response = await _client.PostSceneRequestAsync(_playId, nameof(ProviderUserScene), new ProviderUserScene.Request
        {
            UserId = userId,
            ProviderId = Guid.NewGuid(),
            ProviderKeyB64 = "unused",
            ProviderUserType = ProviderUserType.ServiceUser,
            ProviderUserStatusType = ProviderUserStatusType.Accepted
        });

        await AssertSelfHostRejectionAsync(response, "Provider users");
        Assert.False(await _factory.QueryAsync(db => db.ProviderUsers.AnyAsync(pu => pu.UserId == userId)));
    }

    [Fact]
    public async Task MigrationCohortExportScene_SelfHosted_ReturnsBadRequestWithoutWrites()
    {
        var cohortName = $"self-host-{Guid.NewGuid():N}";
        var namePrefix = $"sh-{Guid.NewGuid():N}-";

        var response = await _client.PostSceneRequestAsync(_playId, nameof(MigrationCohortExportScene), new MigrationCohortExportScene.Request
        {
            CohortName = cohortName,
            OrgCount = 1,
            NamePrefix = namePrefix
        });

        await AssertSelfHostRejectionAsync(response, "Plan migration cohorts");
        Assert.False(await _factory.QueryAsync(db => db.OrganizationPlanMigrationCohorts.AnyAsync(c => c.Name == cohortName)));
        Assert.False(await _factory.QueryAsync(db => db.Organizations.AnyAsync(o => o.Name.StartsWith(namePrefix))));
    }

    [Fact]
    public async Task SingleOrganizationScene_SelfHostedWithGatewayCustomerId_ReturnsBadRequestWithoutWrites()
    {
        var ownerUserId = await SeedUserAsync();
        var name = $"Self-hosted Org {Guid.NewGuid():N}";

        var response = await _client.PostSceneRequestAsync(_playId, nameof(SingleOrganizationScene), new SingleOrganizationScene.Request
        {
            OwnerUserId = ownerUserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = name,
            Domain = $"selfhost-{Guid.NewGuid():N}.example.com",
            Seats = 10,
            GatewayCustomerId = "cus_selfhost"
        });

        await AssertSelfHostRejectionAsync(response, "Stripe gateway IDs");
        Assert.False(await _factory.QueryAsync(db => db.Organizations.AnyAsync(o => o.Name == name)));
    }

    [Fact]
    public async Task SingleOrganizationScene_SelfHostedWithoutGatewayIds_Succeeds()
    {
        var ownerUserId = await SeedUserAsync();

        var result = await _client.PostSceneAsync(_playId, nameof(SingleOrganizationScene), new SingleOrganizationScene.Request
        {
            OwnerUserId = ownerUserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "Self-hosted Org",
            Domain = $"selfhost-{Guid.NewGuid():N}.example.com",
            Seats = 10
        });

        var organizationId = result.GetProperty("organizationId").GetGuid();
        Assert.True(await _factory.QueryAsync(db => db.Organizations.AnyAsync(o => o.Id == organizationId)));
    }

    [Fact]
    public async Task SingleUserScene_SelfHostedWithGatewaySubscriptionId_ReturnsBadRequestWithoutWrites()
    {
        var email = $"selfhost-{Guid.NewGuid():N}@example.com";

        var response = await _client.PostSceneRequestAsync(_playId, nameof(SingleUserScene), new SingleUserScene.Request
        {
            Email = email,
            Password = "asdfasdfasdf",
            GatewaySubscriptionId = "sub_selfhost"
        });

        await AssertSelfHostRejectionAsync(response, "Stripe gateway IDs");
        Assert.False(await _factory.QueryAsync(db => db.Users.AnyAsync(u => u.Email == email)));
    }

    private static async Task AssertSelfHostRejectionAsync(HttpResponseMessage response, string subject)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(subject, body);
        Assert.Contains("globalSettings:selfHosted", body);
    }

    private async Task<Guid> SeedUserAsync() => (await _client.SeedUserAsync(_playId)).UserId;
}
