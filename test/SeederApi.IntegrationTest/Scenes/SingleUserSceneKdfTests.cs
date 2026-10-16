using System.Net;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class SingleUserSceneKdfTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public SingleUserSceneKdfTests(InPlaySeederApiApplicationFactory factory)
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

    [Theory]
    [InlineData(null, 5_000)]
    [InlineData(5_000, 5_000)]
    [InlineData(600_000, 600_000)]
    public async Task SeedEndpoint_SingleUser_UsesRequestedKdfIterations(int? requested, int expected)
    {
        var response = await PostSingleUserAsync(requested);

        response.EnsureSuccessStatusCode();
        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        var result = (JsonElement)model!.Result!;
        Assert.Equal(expected, result.GetProperty("kdfIterations").GetInt32());

        var userId = result.GetProperty("userId").GetGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(expected, user.KdfIterations);
    }

    [Theory]
    [InlineData(4_999)]
    [InlineData(2_000_001)]
    public async Task SeedEndpoint_SingleUser_OutOfRangeKdfIterations_ReturnsBadRequest(int requested)
    {
        var response = await PostSingleUserAsync(requested);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("KdfIterations", await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> PostSingleUserAsync(int? kdfIterations) =>
        _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(SingleUserScene),
            Arguments = JsonSerializer.SerializeToElement(new SingleUserScene.Request
            {
                Email = $"kdf-{Guid.NewGuid():N}@example.com",
                Password = "asdfasdfasdf",
                KdfIterations = kdfIterations
            })
        }, Guid.NewGuid().ToString());
}
