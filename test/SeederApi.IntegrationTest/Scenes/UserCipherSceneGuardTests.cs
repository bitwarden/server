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

    [Fact]
    public async Task UserLoginCipherScene_FolderOwnedByAnotherUser_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserAsync(playId);
        var otherUser = await SeedUserAsync(playId);
        var otherUserFolderId = await SeedFolderAsync(playId, otherUser);

        var response = await _client.PostSceneRequestAsync(playId, nameof(UserLoginCipherScene), LoginRequest(user, otherUserFolderId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not owned by user", await response.Content.ReadAsStringAsync());
        Assert.False(await _factory.QueryAsync(db => db.Ciphers.AnyAsync(c => c.UserId == user.UserId)));
    }

    [Fact]
    public async Task UserLoginCipherScene_OwnFolder_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserAsync(playId);
        var folderId = await SeedFolderAsync(playId, user);

        var result = await _client.PostSceneAsync(playId, nameof(UserLoginCipherScene), LoginRequest(user, folderId));

        var cipherId = result.GetProperty("cipherId").GetGuid();
        Assert.True(await _factory.QueryAsync(db => db.Ciphers.AnyAsync(c => c.Id == cipherId && c.UserId == user.UserId)));
    }

    private static UserLoginCipherScene.Request LoginRequest(SeededUser user, Guid folderId) => new()
    {
        UserId = user.UserId,
        UserKeyB64 = user.UserKeyB64,
        Name = "Login",
        FolderId = folderId
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
