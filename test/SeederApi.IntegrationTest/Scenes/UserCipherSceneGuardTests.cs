using System.Net;
using System.Text.Json;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
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

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = template,
            Arguments = UserCipherRequest(template, user, otherUserFolderId)
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not owned by user", await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.False(await db.Ciphers.AnyAsync(c => c.UserId == user.UserId));
    }

    [Theory]
    [MemberData(nameof(UserCipherScenes))]
    public async Task UserCipherScene_OwnFolder_Succeeds(string template)
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserAsync(playId);
        var folderId = await SeedFolderAsync(playId, user);

        var result = await PostSceneAsync(playId, template, UserCipherRequest(template, user, folderId));

        var cipherId = result.GetProperty("cipherId").GetGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.True(await db.Ciphers.AnyAsync(c => c.Id == cipherId && c.UserId == user.UserId));
    }

    private static JsonElement UserCipherRequest(string template, SeededUser user, Guid folderId) =>
        template switch
        {
            nameof(UserLoginCipherScene) => JsonSerializer.SerializeToElement(new UserLoginCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Login",
                FolderId = folderId
            }),
            nameof(UserBankAccountCipherScene) => JsonSerializer.SerializeToElement(new UserBankAccountCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Bank",
                FolderId = folderId
            }),
            nameof(UserCardCipherScene) => JsonSerializer.SerializeToElement(new UserCardCipherScene.Request
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
            }),
            nameof(UserDriversLicenseCipherScene) => JsonSerializer.SerializeToElement(new UserDriversLicenseCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "License",
                FolderId = folderId
            }),
            nameof(UserIdentityCipherScene) => JsonSerializer.SerializeToElement(new UserIdentityCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Identity",
                FolderId = folderId
            }),
            nameof(UserPassportCipherScene) => JsonSerializer.SerializeToElement(new UserPassportCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Passport",
                FolderId = folderId
            }),
            nameof(UserSecureNoteCipherScene) => JsonSerializer.SerializeToElement(new UserSecureNoteCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "Note",
                FolderId = folderId
            }),
            nameof(UserSshKeyCipherScene) => JsonSerializer.SerializeToElement(new UserSshKeyCipherScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                Name = "SSH",
                FolderId = folderId
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
        };

    private record SeededUser(Guid UserId, string UserKeyB64);

    private async Task<SeededUser> SeedUserAsync(string playId)
    {
        var result = await PostSceneAsync(playId, nameof(SingleUserScene), JsonSerializer.SerializeToElement(
            new SingleUserScene.Request
            {
                Email = $"user-{Guid.NewGuid():N}@example.com",
                Password = "asdfasdfasdf",
                EmailVerified = true
            }));

        return new SeededUser(result.GetProperty("userId").GetGuid(), result.GetProperty("decryptedKeyB64").GetString()!);
    }

    private async Task<Guid> SeedFolderAsync(string playId, SeededUser user)
    {
        var result = await PostSceneAsync(playId, nameof(UserFolderScene), JsonSerializer.SerializeToElement(
            new UserFolderScene.Request
            {
                UserId = user.UserId,
                UserKeyB64 = user.UserKeyB64,
                FolderName = "Folder"
            }));

        return result.GetProperty("folderId").GetGuid();
    }

    private async Task<JsonElement> PostSceneAsync(string playId, string template, JsonElement arguments)
    {
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = template,
            Arguments = arguments
        }, playId);

        response.EnsureSuccessStatusCode();

        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(model);
        Assert.NotNull(model!.Result);
        return (JsonElement)model.Result!;
    }
}
