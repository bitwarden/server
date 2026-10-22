using System.Net;
using Bit.Core.Billing.Enums;
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
    public async Task OrganizationCollectionScene_ForeignOrganizationUser_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var foreignOrg = await SeedOrganizationAsync(playId);

        var response = await _client.PostSceneRequestAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Engineering",
            Users = [Access(foreignOrg.OrganizationUserId)]
        });

        await AssertBadRequestAsync(response, "not in organization");
        Assert.False(await _factory.QueryAsync(db => db.Collections.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationCollectionScene_ForeignGroup_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var foreignOrg = await SeedOrganizationAsync(playId);
        var foreignGroupId = await SeedGroupAsync(playId, foreignOrg.OrganizationId);

        var response = await _client.PostSceneRequestAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Engineering",
            Groups = [Access(foreignGroupId)]
        });

        await AssertBadRequestAsync(response, "not in organization");
        Assert.False(await _factory.QueryAsync(db => db.Collections.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationCollectionScene_SameOrganizationUsersAndGroups_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
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
    public async Task OrganizationCollectionScene_DuplicateIdenticalUsersAndGroups_LinksEachOnce()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var groupId = await SeedGroupAsync(playId, org.OrganizationId);

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Engineering",
            Users = [Access(org.OrganizationUserId), Access(org.OrganizationUserId)],
            Groups = [Access(groupId), Access(groupId)]
        });

        var collectionId = result.GetProperty("collectionId").GetGuid();
        Assert.Equal(1, await _factory.QueryAsync(db => db.CollectionUsers.CountAsync(cu =>
            cu.CollectionId == collectionId && cu.OrganizationUserId == org.OrganizationUserId)));
        Assert.Equal(1, await _factory.QueryAsync(db => db.CollectionGroups.CountAsync(cg =>
            cg.CollectionId == collectionId && cg.GroupId == groupId)));
    }

    [Fact]
    public async Task OrganizationCollectionScene_ConflictingUserAccess_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);

        var response = await _client.PostSceneRequestAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Engineering",
            Users = [Access(org.OrganizationUserId), Access(org.OrganizationUserId, readOnly: true)]
        });

        await AssertBadRequestAsync(response, "Conflicting access settings");
        Assert.False(await _factory.QueryAsync(db => db.Collections.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationGroupScene_SameOrganizationUsersAndCollections_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
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
    public async Task OrganizationGroupScene_DuplicateIdenticalCollections_LinksCollectionOnce()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var collectionId = await SeedCollectionAsync(playId, org);

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationGroupScene), new OrganizationGroupScene.Request
        {
            OrganizationId = org.OrganizationId,
            Name = "Admins",
            Collections = [GroupAccess(collectionId), GroupAccess(collectionId)]
        });

        var groupId = result.GetProperty("groupId").GetGuid();
        Assert.Equal(1, await _factory.QueryAsync(db => db.CollectionGroups.CountAsync(cg =>
            cg.CollectionId == collectionId && cg.GroupId == groupId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_ForeignCollection_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var foreignOrg = await SeedOrganizationAsync(playId);
        var foreignCollectionId = await SeedCollectionAsync(playId, foreignOrg);

        var response = await _client.PostSceneRequestAsync(playId, nameof(OrganizationLoginCipherScene),
            OrganizationLoginRequest(org, [foreignCollectionId], org.Owner.UserId, folderId: null));

        await AssertBadRequestAsync(response, "not in organization");
        Assert.False(await _factory.QueryAsync(db => db.Ciphers.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_DuplicateCollectionIds_LinksCollectionOnce()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var collectionId = await SeedCollectionAsync(playId, org);

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationLoginCipherScene),
            OrganizationLoginRequest(org, [collectionId, collectionId], org.Owner.UserId, folderId: null));

        var cipherId = result.GetProperty("cipherId").GetGuid();
        Assert.Equal(1, await _factory.QueryAsync(db => db.CollectionCiphers.CountAsync(cc =>
            cc.CipherId == cipherId && cc.CollectionId == collectionId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_UserNotAMember_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var outsider = await SeedUserAsync(playId);
        var collectionId = await SeedCollectionAsync(playId, org);

        var response = await _client.PostSceneRequestAsync(playId, nameof(OrganizationLoginCipherScene),
            OrganizationLoginRequest(org, [collectionId], outsider.UserId, folderId: null));

        await AssertBadRequestAsync(response, "not confirmed members");
        Assert.False(await _factory.QueryAsync(db => db.Ciphers.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_ConfirmedMemberWithOwnFolder_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var folderId = await SeedFolderAsync(playId, org.Owner);
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
        var org = await SeedOrganizationAsync(playId);
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

    private static OrganizationCollectionScene.AccessSelectionRequest Access(Guid id, bool readOnly = false) =>
        new() { Id = id, ReadOnly = readOnly, HidePasswords = false, Manage = false };

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

    private record SeededUser(Guid UserId, string UserKeyB64);

    private record SeededOrganization(
        Guid OrganizationId, Guid OrganizationUserId, string OrganizationKeyB64, SeededUser Owner);

    private async Task<SeededUser> SeedUserAsync(string playId)
    {
        var result = await _client.PostSceneAsync(playId, nameof(SingleUserScene), new SingleUserScene.Request
        {
            Email = $"user-{Guid.NewGuid():N}@example.com",
            Password = "asdfasdfasdf",
            EmailVerified = true
        });

        return new SeededUser(result.GetProperty("userId").GetGuid(), result.GetProperty("decryptedKeyB64").GetString()!);
    }

    private async Task<SeededOrganization> SeedOrganizationAsync(string playId)
    {
        var owner = await SeedUserAsync(playId);
        var result = await _client.PostSceneAsync(playId, nameof(SingleOrganizationScene), new SingleOrganizationScene.Request
        {
            OwnerUserId = owner.UserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "Guard Org",
            Domain = $"guard-{Guid.NewGuid():N}.example.com",
            Seats = 10
        });

        return new SeededOrganization(
            result.GetProperty("organizationId").GetGuid(),
            result.GetProperty("organizationUserId").GetGuid(),
            result.GetProperty("organizationKeyB64").GetString()!,
            owner);
    }

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

    private async Task<Guid> SeedFolderAsync(string playId, SeededUser user)
    {
        var result = await _client.PostSceneAsync(playId, nameof(UserFolderScene), new UserFolderScene.Request
        {
            UserId = user.UserId,
            UserKeyB64 = user.UserKeyB64,
            FolderName = "Folder"
        });

        return result.GetProperty("folderId").GetGuid();
    }

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string expectedMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedMessage, await response.Content.ReadAsStringAsync());
    }
}
