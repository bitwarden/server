using System.Net;
using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class OrganizationAccessRuleSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public OrganizationAccessRuleSceneTests(InPlaySeederApiApplicationFactory factory)
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
    public async Task SeedAsync_InvalidCidr_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await _client.SeedOrganizationAsync(playId);
        var request = Rule(org.OrganizationId, "Office");
        request.IpAllowlistCidrs = ["10.0.0.0/33"];

        var response = await _client.PostSceneRequestAsync(playId, nameof(OrganizationAccessRuleScene), request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid CIDR", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await CountRulesAsync(org.OrganizationId));
    }

    private static OrganizationAccessRuleScene.Request Rule(Guid organizationId, string name) =>
        new() { OrganizationId = organizationId, Name = name };

    private Task<int> CountRulesAsync(Guid organizationId) =>
        _factory.QueryAsync(db => db.AccessRules.CountAsync(r => r.OrganizationId == organizationId));
}
