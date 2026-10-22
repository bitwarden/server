using System.Net;
using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class UserCipherSceneGuardTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public UserCipherSceneGuardTests(InPlaySeederApiApplicationFactory factory)
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

    public static TheoryData<string> UserCipherScenes() => new()
    {
        nameof(UserLoginCipherScene),
        nameof(UserBankAccountCipherScene),
        nameof(UserCardCipherScene),
        nameof(UserDriversLicenseCipherScene),
        nameof(UserIdentityCipherScene),
        nameof(UserPassportCipherScene),
        nameof(UserSecureNoteCipherScene),
        nameof(UserSshKeyCipherScene)
    };

    [Theory]
    [MemberData(nameof(UserCipherScenes))]
    public async Task UserCipherScene_FolderOwnedByAnotherUser_ReturnsBadRequestWithoutWrites(string template)
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserAsync(playId);
        var otherUser = await SeedUserAsync(playId);
        var otherUserFolderId = await SeedFolderAsync(playId, otherUser);

        var response = await _client.PostSceneRequestAsync(playId, template, UserCipherRequest(template, user, otherUserFolderId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not owned by user", await response.Content.ReadAsStringAsync());
        Assert.False(await _factory.QueryAsync(db => db.Ciphers.AnyAsync(c => c.UserId == user.UserId)));
    }

    [Theory]
    [MemberData(nameof(UserCipherScenes))]
    public async Task UserCipherScene_OwnFolder_Succeeds(string template)
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserAsync(playId);
        var folderId = await SeedFolderAsync(playId, user);

        var result = await _client.PostSceneAsync(playId, template, UserCipherRequest(template, user, folderId));

        var cipherId = result.GetProperty("cipherId").GetGuid();
        Assert.True(await _factory.QueryAsync(db => db.Ciphers.AnyAsync(c => c.Id == cipherId && c.UserId == user.UserId)));
    }

    private static object UserCipherRequest(string template, SeededUser user, Guid folderId) =>
        template switch
        {
            nameof(UserLoginCipherScene) => new UserLoginCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Login",
                FolderId = folderId
            },
            nameof(UserBankAccountCipherScene) => new UserBankAccountCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Bank",
                FolderId = folderId
            },
            nameof(UserCardCipherScene) => new UserCardCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Card",
                CardholderName = "Jane Doe",
                Number = "4111111111111111",
                ExpMonth = "12",
                ExpYear = "2030",
                Code = "123",
                FolderId = folderId
            },
            nameof(UserDriversLicenseCipherScene) => new UserDriversLicenseCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "License",
                FolderId = folderId
            },
            nameof(UserIdentityCipherScene) => new UserIdentityCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Identity",
                FolderId = folderId
            },
            nameof(UserPassportCipherScene) => new UserPassportCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Passport",
                FolderId = folderId
            },
            nameof(UserSecureNoteCipherScene) => new UserSecureNoteCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Note",
                FolderId = folderId
            },
            nameof(UserSshKeyCipherScene) => new UserSshKeyCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "SSH",
                FolderId = folderId
            },
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
        };

    private record SeededUser(Guid UserId, string UserKeyB64);

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
}
