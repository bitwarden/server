using System.Net;
using System.Text.Json;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
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

        var response = await PostAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Engineering",
            Users = [Access(foreignOrg.OrganizationUserId)]
        });

        await AssertBadRequestAsync(response, "not in organization");
        Assert.False(await QueryAsync(db => db.Collections.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationCollectionScene_ForeignGroup_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var foreignOrg = await SeedOrganizationAsync(playId);
        var foreignGroupId = await SeedGroupAsync(playId, foreignOrg.OrganizationId);

        var response = await PostAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Engineering",
            Groups = [Access(foreignGroupId)]
        });

        await AssertBadRequestAsync(response, "not in organization");
        Assert.False(await QueryAsync(db => db.Collections.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationCollectionScene_SameOrganizationUsersAndGroups_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var groupId = await SeedGroupAsync(playId, org.OrganizationId);

        var result = await PostSceneAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Engineering",
            Users = [Access(org.OrganizationUserId)],
            Groups = [Access(groupId)]
        });

        var collectionId = result.GetProperty("collectionId").GetGuid();
        Assert.True(await QueryAsync(db => db.Collections.AnyAsync(c =>
            c.Id == collectionId && c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationGroupScene_ForeignOrganizationUser_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var foreignOrg = await SeedOrganizationAsync(playId);

        var response = await PostAsync(playId, nameof(OrganizationGroupScene), new OrganizationGroupScene.Request
        {
            OrganizationId = org.OrganizationId,
            Name = "Admins",
            OrganizationUserIds = [foreignOrg.OrganizationUserId]
        });

        await AssertBadRequestAsync(response, "not in organization");
        Assert.False(await QueryAsync(db => db.Groups.AnyAsync(g => g.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationGroupScene_ForeignCollection_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var foreignOrg = await SeedOrganizationAsync(playId);
        var foreignCollectionId = await SeedCollectionAsync(playId, foreignOrg);

        var response = await PostAsync(playId, nameof(OrganizationGroupScene), new OrganizationGroupScene.Request
        {
            OrganizationId = org.OrganizationId,
            Name = "Admins",
            Collections = [GroupAccess(foreignCollectionId)]
        });

        await AssertBadRequestAsync(response, "not in organization");
        Assert.False(await QueryAsync(db => db.Groups.AnyAsync(g => g.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationGroupScene_SameOrganizationUsersAndCollections_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var collectionId = await SeedCollectionAsync(playId, org);

        var result = await PostSceneAsync(playId, nameof(OrganizationGroupScene), new OrganizationGroupScene.Request
        {
            OrganizationId = org.OrganizationId,
            Name = "Admins",
            OrganizationUserIds = [org.OrganizationUserId],
            Collections = [GroupAccess(collectionId)]
        });

        var groupId = result.GetProperty("groupId").GetGuid();
        Assert.True(await QueryAsync(db => db.Groups.AnyAsync(g =>
            g.Id == groupId && g.OrganizationId == org.OrganizationId)));
    }

    public static TheoryData<string> OrganizationCipherScenes() => new()
    {
        nameof(OrganizationLoginCipherScene),
        nameof(OrganizationBankAccountCipherScene),
        nameof(OrganizationCardCipherScene),
        nameof(OrganizationDriversLicenseCipherScene),
        nameof(OrganizationIdentityCipherScene),
        nameof(OrganizationPassportCipherScene),
        nameof(OrganizationSecureNoteCipherScene)
    };

    [Theory]
    [MemberData(nameof(OrganizationCipherScenes))]
    public async Task OrganizationCipherScene_ForeignCollection_ReturnsBadRequestWithoutWrites(string template)
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var foreignOrg = await SeedOrganizationAsync(playId);
        var foreignCollectionId = await SeedCollectionAsync(playId, foreignOrg);

        var response = await PostAsync(playId, template, OrganizationCipherRequest(template, org, foreignCollectionId));

        await AssertBadRequestAsync(response, "not in organization");
        Assert.False(await QueryAsync(db => db.Ciphers.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Theory]
    [MemberData(nameof(OrganizationCipherScenes))]
    public async Task OrganizationCipherScene_SameOrganizationCollection_Succeeds(string template)
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var collectionId = await SeedCollectionAsync(playId, org);

        var result = await PostSceneAsync(playId, template, OrganizationCipherRequest(template, org, collectionId));

        var cipherId = result.GetProperty("cipherId").GetGuid();
        Assert.True(await QueryAsync(db => db.Ciphers.AnyAsync(c =>
            c.Id == cipherId && c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_UserNotAMember_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var outsider = await SeedUserAsync(playId);
        var collectionId = await SeedCollectionAsync(playId, org);

        var response = await PostAsync(playId, nameof(OrganizationLoginCipherScene),
            OrganizationLoginRequest(org, collectionId, outsider.UserId, folderId: null));

        await AssertBadRequestAsync(response, "not confirmed members");
        Assert.False(await QueryAsync(db => db.Ciphers.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_AcceptedMember_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var member = await SeedUserAsync(playId);
        await PostSceneAsync(playId, nameof(OrganizationUserScene), new OrganizationUserScene.Request
        {
            UserId = member.UserId,
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            OrganizationUserType = OrganizationUserType.User,
            OrganizationUserStatusType = OrganizationUserStatusType.Accepted
        });
        var collectionId = await SeedCollectionAsync(playId, org);

        var response = await PostAsync(playId, nameof(OrganizationLoginCipherScene),
            OrganizationLoginRequest(org, collectionId, member.UserId, folderId: null));

        await AssertBadRequestAsync(response, "not confirmed members");
        Assert.False(await QueryAsync(db => db.Ciphers.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_FolderOwnedByAnotherUser_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var otherUser = await SeedUserAsync(playId);
        var otherUserFolderId = await SeedFolderAsync(playId, otherUser);
        var collectionId = await SeedCollectionAsync(playId, org);

        var response = await PostAsync(playId, nameof(OrganizationLoginCipherScene),
            OrganizationLoginRequest(org, collectionId, org.Owner.UserId, otherUserFolderId));

        await AssertBadRequestAsync(response, "not owned by user");
        Assert.False(await QueryAsync(db => db.Ciphers.AnyAsync(c => c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_ConfirmedMemberWithOwnFolder_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var folderId = await SeedFolderAsync(playId, org.Owner);
        var collectionId = await SeedCollectionAsync(playId, org);

        var result = await PostSceneAsync(playId, nameof(OrganizationLoginCipherScene),
            OrganizationLoginRequest(org, collectionId, org.Owner.UserId, folderId));

        var cipherId = result.GetProperty("cipherId").GetGuid();
        Assert.True(await QueryAsync(db => db.Ciphers.AnyAsync(c =>
            c.Id == cipherId && c.OrganizationId == org.OrganizationId)));
    }

    [Fact]
    public async Task OrganizationEventScene_ActingUserNotAMember_ReturnsBadRequest()
    {
        var playId = Guid.NewGuid().ToString();
        var org = await SeedOrganizationAsync(playId);
        var foreignOrg = await SeedOrganizationAsync(playId);

        var response = await PostAsync(playId, nameof(OrganizationEventScene), new OrganizationEventScene.Request
        {
            OrganizationId = org.OrganizationId,
            ActingUserId = foreignOrg.Owner.UserId
        });

        await AssertBadRequestAsync(response, "not confirmed members");
    }

    private static OrganizationCollectionScene.AccessSelectionRequest Access(Guid id) =>
        new() { Id = id, ReadOnly = false, HidePasswords = false, Manage = false };

    private static OrganizationGroupScene.AccessSelectionRequest GroupAccess(Guid id) =>
        new() { Id = id, ReadOnly = false, HidePasswords = false, Manage = false };

    private static OrganizationLoginCipherScene.Request OrganizationLoginRequest(
        SeededOrganization org, Guid collectionId, Guid userId, Guid? folderId) => new()
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            CollectionIds = [collectionId],
            UserId = userId,
            Name = "Login",
            FolderId = folderId
        };

    private static object OrganizationCipherRequest(string template, SeededOrganization org, Guid collectionId) =>
        template switch
        {
            nameof(OrganizationLoginCipherScene) => OrganizationLoginRequest(org, collectionId, org.Owner.UserId, null),
            nameof(OrganizationBankAccountCipherScene) => new OrganizationBankAccountCipherScene.Request
            {
                OrganizationId = org.OrganizationId,
                OrganizationKeyB64 = org.OrganizationKeyB64,
                CollectionIds = [collectionId],
                Name = "Bank"
            },
            nameof(OrganizationCardCipherScene) => new OrganizationCardCipherScene.Request
            {
                OrganizationId = org.OrganizationId,
                OrganizationKeyB64 = org.OrganizationKeyB64,
                CollectionIds = [collectionId],
                Name = "Card",
                CardholderName = "Jane Doe",
                Number = "4111111111111111",
                ExpMonth = "12",
                ExpYear = "2030",
                Code = "123"
            },
            nameof(OrganizationDriversLicenseCipherScene) => new OrganizationDriversLicenseCipherScene.Request
            {
                OrganizationId = org.OrganizationId,
                OrganizationKeyB64 = org.OrganizationKeyB64,
                CollectionIds = [collectionId],
                Name = "License"
            },
            nameof(OrganizationIdentityCipherScene) => new OrganizationIdentityCipherScene.Request
            {
                OrganizationId = org.OrganizationId,
                OrganizationKeyB64 = org.OrganizationKeyB64,
                CollectionIds = [collectionId],
                Name = "Identity"
            },
            nameof(OrganizationPassportCipherScene) => new OrganizationPassportCipherScene.Request
            {
                OrganizationId = org.OrganizationId,
                OrganizationKeyB64 = org.OrganizationKeyB64,
                CollectionIds = [collectionId],
                Name = "Passport"
            },
            nameof(OrganizationSecureNoteCipherScene) => new OrganizationSecureNoteCipherScene.Request
            {
                OrganizationId = org.OrganizationId,
                OrganizationKeyB64 = org.OrganizationKeyB64,
                CollectionIds = [collectionId],
                Name = "Note"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
        };

    private record SeededUser(Guid UserId, string UserKeyB64);

    private record SeededOrganization(
        Guid OrganizationId, Guid OrganizationUserId, string OrganizationKeyB64, SeededUser Owner);

    private async Task<SeededUser> SeedUserAsync(string playId)
    {
        var result = await PostSceneAsync(playId, nameof(SingleUserScene), new SingleUserScene.Request
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
        var result = await PostSceneAsync(playId, nameof(SingleOrganizationScene), new SingleOrganizationScene.Request
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
        var result = await PostSceneAsync(playId, nameof(OrganizationGroupScene), new OrganizationGroupScene.Request
        {
            OrganizationId = organizationId,
            Name = "Group"
        });

        return result.GetProperty("groupId").GetGuid();
    }

    private async Task<Guid> SeedCollectionAsync(string playId, SeededOrganization org)
    {
        var result = await PostSceneAsync(playId, nameof(OrganizationCollectionScene), new OrganizationCollectionScene.Request
        {
            OrganizationId = org.OrganizationId,
            OrganizationKeyB64 = org.OrganizationKeyB64,
            Name = "Collection"
        });

        return result.GetProperty("collectionId").GetGuid();
    }

    private async Task<Guid> SeedFolderAsync(string playId, SeededUser user)
    {
        var result = await PostSceneAsync(playId, nameof(UserFolderScene), new UserFolderScene.Request
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

    private async Task<T> QueryAsync<T>(Func<DatabaseContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<DatabaseContext>());
    }

    private Task<HttpResponseMessage> PostAsync(string playId, string template, object arguments) =>
        _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = template,
            Arguments = JsonSerializer.SerializeToElement(arguments, arguments.GetType())
        }, playId);

    private async Task<JsonElement> PostSceneAsync(string playId, string template, object arguments)
    {
        var response = await PostAsync(playId, template, arguments);
        response.EnsureSuccessStatusCode();

        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(model);
        Assert.NotNull(model!.Result);
        return (JsonElement)model.Result!;
    }
}
