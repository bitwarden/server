using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class OrganizationVaultSceneGuardTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public OrganizationVaultSceneGuardTests(InPlaySeederApiApplicationFactory factory)
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
    public async Task OrganizationCollectionScene_SameOrganizationUsersAndGroups_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await _client.SeedOrganizationAsync(playId);
        var groupId = await SeedGroupAsync(playId, org.OrganizationId);

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Engineering",
            Users = [Access(org.OrganizationUserId)],
            Groups = [Access(groupId)]
        });

        var collectionId = result.GetProperty("collectionId").GetGuid();
        Assert.True(await _factory.QueryAsync(db => db.Collections.AnyAsync(c =>
            c.Id == collectionId && c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationGroupScene_SameOrganizationUsersAndCollections_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await _client.SeedOrganizationAsync(playId);
        var collectionId = await SeedCollectionAsync(playId, org);

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationGroupScene), new OrganizationGroupScene.Request
        {
            OrganizationId = org.OrganizationId,
            Name = "Admins",
            OrganizationUserIds = [org.OrganizationUserId],
            Collections = [GroupAccess(collectionId)]
        });

        var groupId = result.GetProperty("groupId").GetGuid();
        Assert.True(await _factory.QueryAsync(db => db.Groups.AnyAsync(g =>
            g.Id == groupId && g.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_ConfirmedMemberWithOwnFolder_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await _client.SeedOrganizationAsync(playId);
        var folderId = await _client.SeedFolderAsync(playId, org.Owner);
        var collectionId = await SeedCollectionAsync(playId, org);

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationLoginCipherScene),
            OrganizationLoginRequest(org, [collectionId], org.Owner.UserId, folderId));

        var cipherId = result.GetProperty("cipherId").GetGuid();
        Assert.True(await _factory.QueryAsync(db => db.Ciphers.AnyAsync(c =>
            c.Id == cipherId && c.OrganizationId == org.OrganizationId)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrganizationEventScene_ConfirmedOrNoActingUser_Succeeds(bool withActingUser)
    {
        var playId = Guid.NewGuid().ToString();
        var org = await _client.SeedOrganizationAsync(playId);
        Guid? actingUserId = withActingUser ? org.Owner.UserId : null;

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationEventScene), new OrganizationEventScene.Request
        {
            OrganizationId = org.OrganizationId,
            ActingUserId = actingUserId
        });

        Assert.Equal(2, result.GetProperty("seededCount").GetInt32());
        Assert.Equal(2, await _factory.QueryAsync(db => db.Events.CountAsync(e =>
            e.OrganizationId == org.OrganizationId && e.ActingUserId == actingUserId)));
    }

    private static OrganizationCollectionScene.AccessSelectionRequest Access(Guid id) =>
        new() { Id = id, ReadOnly = false, HidePasswords = false, Manage = false };

    private static OrganizationGroupScene.AccessSelectionRequest GroupAccess(Guid id) =>
        new() { Id = id, ReadOnly = false, HidePasswords = false, Manage = false };

    private static OrganizationLoginCipherScene.Request OrganizationLoginRequest(
        SeededOrganization org, IEnumerable<Guid> collectionIds, Guid userId, Guid? folderId) => new()
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            CollectionIds = collectionIds,
            UserId = userId,
            Name = "Login",
            FolderId = folderId
        };

    private async Task<Guid> SeedGroupAsync(string playId, Guid organizationId)
    {
        var result = await _client.PostSceneAsync(playId, nameof(OrganizationGroupScene), new OrganizationGroupScene.Request
        {
            OrganizationId = organizationId,
            Name = "Group"
        });

        return result.GetProperty("groupId").GetGuid();
    }

    private async Task<Guid> SeedCollectionAsync(string playId, SeededOrganization org)
    {
        var result = await _client.PostSceneAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Collection"
        });

        return result.GetProperty("collectionId").GetGuid();
    }
}
