using System.Net;
using Bit.Seeder.Scenes;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class SsoUserLinkSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;
    private readonly string _playId = Guid.NewGuid().ToString();

    public SsoUserLinkSceneTests(InPlaySeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_factory.Username, _factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        var deleteResponse = await _client.DeleteAsync($"/seed/{_playId}");
        deleteResponse.EnsureSuccessStatusCode();
        _client.Dispose();
    }

    [Fact]
    public async Task SeedAsync_UserIsNotAMemberOfOrganization_ReturnsBadRequestAndWritesNoLink()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);
        var user = await _client.SeedUserAsync(_playId);

        var response = await _client.PostSceneRequestAsync(_playId, nameof(SsoUserLinkScene), new SsoUserLinkScene.Request
        {
            UserId = user.UserId,
            OrganizationId = organization.OrganizationId,
            ExternalId = "non-member-external-id"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var linked = await _factory.QueryAsync(db => db.SsoUsers
            .AnyAsync(su => su.UserId == user.UserId && su.OrganizationId == organization.OrganizationId));
        Assert.False(linked);
    }

    [Fact]
    public async Task SeedAsync_ConfirmedOwner_ReturnsIdOfStoredLink()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);
        var owner = organization.Owner;

        var result = await _client.PostSceneAsync(_playId, nameof(SsoUserLinkScene), new SsoUserLinkScene.Request
        {
            UserId = owner.UserId,
            OrganizationId = organization.OrganizationId,
            ExternalId = "owner-external-id"
        });

        var ssoUserId = result.GetProperty("ssoUserId").GetInt64();
        var stored = await _factory.QueryAsync(db => db.SsoUsers
            .SingleAsync(su => su.OrganizationId == organization.OrganizationId && su.UserId == owner.UserId));
        Assert.Equal(ssoUserId, stored.Id);
    }
}
