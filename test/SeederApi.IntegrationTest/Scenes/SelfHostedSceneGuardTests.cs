using System.Net;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class SelfHostedSceneGuardTests : IAsyncLifetime
{
    private const string Username = "username";
    private const string Password = "pass";

    private readonly SeederApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public SelfHostedSceneGuardTests()
    {
        _factory = new SeederApiApplicationFactory();
        _factory.ConfigureAuth(Username, Password);
        _factory.UpdateConfiguration("globalSettings:selfHosted", "true");
        // AddPush rejects self-hosted startup without an installation id.
        _factory.UpdateConfiguration("globalSettings:installation:id", "10000000-0000-0000-0000-000000000000");
        // Self-hosted defaults data protection keys to /etc/bitwarden, which is not writable on CI.
        _factory.UpdateConfiguration("globalSettings:dataProtection:directory",
            Path.Combine(Path.GetTempPath(), $"seeder-dp-{Guid.NewGuid()}"));
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(Username, Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync("/seed");
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SingleProviderScene_SelfHosted_ReturnsBadRequestWithoutWrites()
    {
        var ownerUserId = await SeedUserAsync();

        var response = await PostAsync(nameof(SingleProviderScene), new SingleProviderScene.Request
        {
            OwnerUserId = ownerUserId,
            Name = "Self-hosted MSP",
            Type = ProviderType.Msp
        });

        await AssertSelfHostRejectionAsync(response, "Providers");
        Assert.False(await QueryAsync(db => db.Providers.AnyAsync()));
        Assert.False(await QueryAsync(db => db.ProviderUsers.AnyAsync()));
    }

    [Fact]
    public async Task ProviderUserScene_SelfHosted_ReturnsBadRequestWithoutWrites()
    {
        var userId = await SeedUserAsync();

        var response = await PostAsync(nameof(ProviderUserScene), new ProviderUserScene.Request
        {
            UserId = userId,
            ProviderId = Guid.NewGuid(),
            ProviderKeyB64 = "unused",
            ProviderUserType = ProviderUserType.ServiceUser,
            ProviderUserStatusType = ProviderUserStatusType.Accepted
        });

        await AssertSelfHostRejectionAsync(response, "Provider users");
        Assert.False(await QueryAsync(db => db.ProviderUsers.AnyAsync()));
    }

    [Fact]
    public async Task MigrationCohortExportScene_SelfHosted_ReturnsBadRequestWithoutWrites()
    {
        var response = await PostAsync(nameof(MigrationCohortExportScene), new MigrationCohortExportScene.Request
        {
            CohortName = $"self-host-{Guid.NewGuid():N}",
            OrgCount = 1
        });

        await AssertSelfHostRejectionAsync(response, "Plan migration cohorts");
        Assert.False(await QueryAsync(db => db.OrganizationPlanMigrationCohorts.AnyAsync()));
        Assert.False(await QueryAsync(db => db.Organizations.AnyAsync()));
    }

    [Theory]
    [InlineData("cus_selfhost", null)]
    [InlineData(null, "sub_selfhost")]
    public async Task SingleOrganizationScene_SelfHostedWithGatewayIds_ReturnsBadRequestWithoutWrites(
        string? gatewayCustomerId, string? gatewaySubscriptionId)
    {
        var ownerUserId = await SeedUserAsync();

        var response = await PostAsync(nameof(SingleOrganizationScene), new SingleOrganizationScene.Request
        {
            OwnerUserId = ownerUserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "Self-hosted Org",
            Domain = $"selfhost-{Guid.NewGuid():N}.example.com",
            Seats = 10,
            GatewayCustomerId = gatewayCustomerId,
            GatewaySubscriptionId = gatewaySubscriptionId
        });

        await AssertSelfHostRejectionAsync(response, "Stripe gateway IDs");
        Assert.False(await QueryAsync(db => db.Organizations.AnyAsync()));
    }

    [Fact]
    public async Task SingleOrganizationScene_SelfHostedWithoutGatewayIds_Succeeds()
    {
        var ownerUserId = await SeedUserAsync();

        var result = await PostSceneAsync(nameof(SingleOrganizationScene), new SingleOrganizationScene.Request
        {
            OwnerUserId = ownerUserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "Self-hosted Org",
            Domain = $"selfhost-{Guid.NewGuid():N}.example.com",
            Seats = 10
        });

        var organizationId = result.GetProperty("organizationId").GetGuid();
        Assert.True(await QueryAsync(db => db.Organizations.AnyAsync(o => o.Id == organizationId)));
    }

    [Theory]
    [InlineData("cus_selfhost", null)]
    [InlineData(null, "sub_selfhost")]
    public async Task SingleUserScene_SelfHostedWithGatewayIds_ReturnsBadRequestWithoutWrites(
        string? gatewayCustomerId, string? gatewaySubscriptionId)
    {
        var response = await PostAsync(nameof(SingleUserScene), new SingleUserScene.Request
        {
            Email = $"selfhost-{Guid.NewGuid():N}@example.com",
            Password = "asdfasdfasdf",
            GatewayCustomerId = gatewayCustomerId,
            GatewaySubscriptionId = gatewaySubscriptionId
        });

        await AssertSelfHostRejectionAsync(response, "Stripe gateway IDs");
        Assert.False(await QueryAsync(db => db.Users.AnyAsync()));
    }

    private static async Task AssertSelfHostRejectionAsync(HttpResponseMessage response, string subject)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(subject, body);
        Assert.Contains("globalSettings:selfHosted", body);
    }

    private async Task<Guid> SeedUserAsync()
    {
        var result = await PostSceneAsync(nameof(SingleUserScene), new SingleUserScene.Request
        {
            Email = $"user-{Guid.NewGuid():N}@example.com",
            Password = "asdfasdfasdf",
            EmailVerified = true
        });

        return result.GetProperty("userId").GetGuid();
    }

    private async Task<T> QueryAsync<T>(Func<DatabaseContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<DatabaseContext>());
    }

    private Task<HttpResponseMessage> PostAsync<TRequest>(string template, TRequest arguments) =>
        _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = template,
            Arguments = JsonSerializer.SerializeToElement(arguments)
        }, Guid.NewGuid().ToString());

    private async Task<JsonElement> PostSceneAsync<TRequest>(string template, TRequest arguments)
    {
        var response = await PostAsync(template, arguments);
        response.EnsureSuccessStatusCode();

        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(model);
        Assert.NotNull(model!.Result);
        return (JsonElement)model.Result!;
    }
}
