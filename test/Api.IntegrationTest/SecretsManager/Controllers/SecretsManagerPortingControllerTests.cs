using System.Net;
using System.Net.Http.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.SecretsManager.Models.Request;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class SecretsManagerPortingControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public SecretsManagerPortingControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_email);
        _organizationHelper = new SecretsManagerOrganizationHelper(_factory, _email);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Import_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var projectsList = new List<SMImportRequestModel.InnerProjectImportRequestModel>();
        var secretsList = new List<SMImportRequestModel.InnerSecretImportRequestModel>();
        var request = new SMImportRequestModel { Projects = projectsList, Secrets = secretsList };

        var response = await _client.PostAsJsonAsync($"sm/{org.Id}/import", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Export_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"sm/{org.Id}/export");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private const string EncString = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private static SMImportRequestModel.InnerSecretImportRequestModel NewSecret(params Guid[] projectIds) => new()
    {
        Id = Guid.NewGuid(),
        Key = EncString,
        Value = EncString,
        Note = EncString,
        ProjectIds = projectIds,
    };

    private async Task<System.Text.Json.JsonElement> ExportAsync(Guid orgId)
    {
        var response = await _client.GetAsync($"sm/{orgId}/export");
        response.EnsureSuccessStatusCode();
        return System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task<Guid> LoginAsSmOwnerAsync()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        return org.Id;
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"secrets\":null}")]
    [InlineData("{\"projects\":[null],\"secrets\":[]}")]
    [InlineData("{\"projects\":[],\"secrets\":[null]}")]
    public async Task Import_MalformedBody_BadRequest(string json)
    {
        var orgId = await LoginAsSmOwnerAsync();

        var response = await _client.PostAsync($"sm/{orgId}/import", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_SecretReferencesNonexistentProject_BadRequest_NothingWritten()
    {
        var orgId = await LoginAsSmOwnerAsync();
        var request = new SMImportRequestModel
        {
            Projects = new[] { new SMImportRequestModel.InnerProjectImportRequestModel { Id = Guid.NewGuid(), Name = EncString } },
            Secrets = new[] { NewSecret(Guid.NewGuid()) },
        };

        var response = await _client.PostAsJsonAsync($"sm/{orgId}/import", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var export = await ExportAsync(orgId);
        Assert.Equal(0, export.GetProperty("projects").GetArrayLength());
        Assert.Equal(0, export.GetProperty("secrets").GetArrayLength());
    }

    [Fact]
    public async Task Import_Valid_Ok()
    {
        var orgId = await LoginAsSmOwnerAsync();
        var projectId = Guid.NewGuid();
        var request = new SMImportRequestModel
        {
            Projects = new[] { new SMImportRequestModel.InnerProjectImportRequestModel { Id = projectId, Name = EncString } },
            Secrets = new[] { NewSecret(projectId), NewSecret() },
        };

        var response = await _client.PostAsJsonAsync($"sm/{orgId}/import", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var export = await ExportAsync(orgId);
        Assert.Equal(1, export.GetProperty("projects").GetArrayLength());
        Assert.Equal(2, export.GetProperty("secrets").GetArrayLength());
    }
}
