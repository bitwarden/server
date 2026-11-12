using Bit.Seeder.Scenes;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class SingleUserNoMasterPasswordSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;
    private readonly string _playId = Guid.NewGuid().ToString();

    public SingleUserNoMasterPasswordSceneTests(InPlaySeederApiApplicationFactory factory)
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
    public async Task SeedAsync_PersistsUserWithoutMasterPasswordButWithKeyPair()
    {
        var result = await _client.PostSceneAsync(_playId, nameof(SingleUserNoMasterPasswordScene),
            new SingleUserNoMasterPasswordScene.Request
            {
                Email = $"nomp-{Guid.NewGuid():N}@example.com",
                EmailVerified = true
            });

        var userId = result.GetProperty("userId").GetGuid();
        var user = await _factory.QueryAsync(db => db.Users.SingleAsync(u => u.Id == userId));
        Assert.Null(user.MasterPassword);
        Assert.Null(user.Key);
        Assert.NotNull(user.PublicKey);
        Assert.NotNull(user.PrivateKey);
    }
}
