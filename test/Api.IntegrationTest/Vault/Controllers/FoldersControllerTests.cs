using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Vault.Models.Request;
using Xunit;

namespace Bit.Api.IntegrationTest.Vault.Controllers;

public class FoldersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _encryptedName =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private const string _otherEncryptedName =
        "2.06CDSJjTZaigYHUuswIq5A==|trxgZl2RCkYrrmCvGE9WNA==|w5p05eI5wsaYeSyWtsAPvBX63vj798kIMxBTfSB0BQg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;

    public FoldersControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _email = await LoginAsNewUserAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PostThenGet_ScopesFolderToCallingUserFromBearerToken()
    {
        var folderId = await CreateFolderAsync();

        var response = await _client.GetAsync($"/folders/{folderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var fetched = await ReadJsonAsync(response);
        Assert.Equal(folderId, fetched.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyCallingUsersFolders()
    {
        await CreateFolderAsync();
        await CreateFolderAsync();
        await LoginAsNewUserAsync();
        var ownFolderId = await CreateFolderAsync();

        var response = await _client.GetAsync("/folders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var list = await ReadJsonAsync(response);
        var ids = list.RootElement.GetProperty("data").EnumerateArray()
            .Select(f => f.GetProperty("id").GetGuid()).ToList();
        Assert.Equal([ownFolderId], ids);
    }

    [Fact]
    public async Task Get_OtherUsersFolder_ReturnsNotFound()
    {
        var folderId = await CreateFolderAsync();
        await LoginAsNewUserAsync();

        var response = await _client.GetAsync($"/folders/{folderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_UpdatesCallingUsersFolder()
    {
        var folderId = await CreateFolderAsync();

        var putResponse = await _client.PutAsJsonAsync($"/folders/{folderId}",
            new FolderRequestModel { Name = _otherEncryptedName });

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var getResponse = await _client.GetAsync($"/folders/{folderId}");
        using var fetched = await ReadJsonAsync(getResponse);
        Assert.Equal(_otherEncryptedName, fetched.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Put_OtherUsersFolder_ReturnsNotFound()
    {
        var folderId = await CreateFolderAsync();
        await LoginAsNewUserAsync();

        var response = await _client.PutAsJsonAsync($"/folders/{folderId}",
            new FolderRequestModel { Name = _otherEncryptedName });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGet_ReturnsNotFound()
    {
        var folderId = await CreateFolderAsync();

        var deleteResponse = await _client.DeleteAsync($"/folders/{folderId}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var getResponse = await _client.GetAsync($"/folders/{folderId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherUsersFolder_ReturnsNotFoundAndLeavesFolderIntact()
    {
        var folderId = await CreateFolderAsync();
        await LoginAsNewUserAsync();

        var deleteResponse = await _client.DeleteAsync($"/folders/{folderId}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        await _loginHelper.LoginAsync(_email);
        var getResponse = await _client.GetAsync($"/folders/{folderId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAll_RemovesOnlyCallingUsersFolders()
    {
        var otherUsersFolderId = await CreateFolderAsync();
        await LoginAsNewUserAsync();
        await CreateFolderAsync();
        await CreateFolderAsync();

        var deleteResponse = await _client.DeleteAsync("/folders/all");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        using var ownList = await ReadJsonAsync(await _client.GetAsync("/folders"));
        Assert.Empty(ownList.RootElement.GetProperty("data").EnumerateArray());
        await _loginHelper.LoginAsync(_email);
        var otherUsersFolder = await _client.GetAsync($"/folders/{otherUsersFolderId}");
        Assert.Equal(HttpStatusCode.OK, otherUsersFolder.StatusCode);
    }

    private async Task<string> LoginAsNewUserAsync()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await _loginHelper.LoginAsync(email);
        return email;
    }

    private async Task<Guid> CreateFolderAsync()
    {
        var response = await _client.PostAsJsonAsync("/folders", new FolderRequestModel { Name = _encryptedName });
        response.EnsureSuccessStatusCode();
        using var created = await ReadJsonAsync(response);
        return created.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
