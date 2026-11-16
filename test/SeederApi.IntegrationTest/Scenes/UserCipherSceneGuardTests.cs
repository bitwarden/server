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
    public async Task UserLoginCipherScene_OwnFolder_Succeeds()
    {
        var playId = Guid.NewGuid().ToString();
        var user = await _client.SeedUserAsync(playId);
        var folderId = await _client.SeedFolderAsync(playId, user);

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
}
