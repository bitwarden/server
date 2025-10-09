using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class SecretVersionsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ISecretRepository _secretRepository;
    private readonly ISecretVersionRepository _secretVersionRepository;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public SecretVersionsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _secretRepository = _factory.GetService<ISecretRepository>();
        _secretVersionRepository = _factory.GetService<ISecretVersionRepository>();
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
    public async Task GetVersionsBySecretId_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var response = await _client.GetAsync($"/secrets/{secret.Id}/versions");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetVersionsBySecretId_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        // Create some versions
        var version1 = await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = _mockEncryptedString,
            VersionDate = DateTime.UtcNow.AddDays(-2)
        });

        var version2 = await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = _mockEncryptedString,
            VersionDate = DateTime.UtcNow.AddDays(-1)
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
        }

        var response = await _client.GetAsync($"/secrets/{secret.Id}/versions");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<SecretVersionResponseModel>>();

        Assert.NotNull(result);
        Assert.Equal(2, result.Data.Count());
    }

    [Fact]
    public async Task GetVersionById_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var version = await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = _mockEncryptedString,
            VersionDate = DateTime.UtcNow
        });

        var response = await _client.GetAsync($"/secret-versions/{version.Id}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SecretVersionResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(version.Id, result.Id);
        Assert.Equal(secret.Id, result.SecretId);
    }

    [Fact]
    public async Task RestoreVersion_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = "OriginalValue",
            Note = _mockEncryptedString
        });

        var version = await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = "OldValue",
            VersionDate = DateTime.UtcNow.AddDays(-1)
        });

        var request = new RestoreSecretVersionRequestModel
        {
            VersionId = version.Id
        };

        var response = await _client.PutAsJsonAsync($"/secrets/{secret.Id}/versions/restore", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SecretResponseModel>();

        Assert.NotNull(result);
        Assert.Equal("OldValue", result.Value);
    }

    [Fact]
    public async Task BulkDelete_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var version1 = await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = _mockEncryptedString,
            VersionDate = DateTime.UtcNow.AddDays(-2)
        });

        var version2 = await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = _mockEncryptedString,
            VersionDate = DateTime.UtcNow.AddDays(-1)
        });

        var ids = new List<Guid> { version1.Id, version2.Id };

        var response = await _client.PostAsJsonAsync("/secret-versions/delete", ids);
        response.EnsureSuccessStatusCode();

        var versions = await _secretVersionRepository.GetManyBySecretIdAsync(secret.Id);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task GetVersionsBySecretId_ReturnsOrderedByVersionDate()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        // Create versions in random order
        await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = "Version2",
            VersionDate = DateTime.UtcNow.AddDays(-1)
        });

        await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = "Version3",
            VersionDate = DateTime.UtcNow
        });

        await _secretVersionRepository.CreateAsync(new SecretVersion
        {
            SecretId = secret.Id,
            Value = "Version1",
            VersionDate = DateTime.UtcNow.AddDays(-2)
        });

        var response = await _client.GetAsync($"/secrets/{secret.Id}/versions");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<SecretVersionResponseModel>>();

        Assert.NotNull(result);
        Assert.Equal(3, result.Data.Count());

        var versions = result.Data.ToList();
        // Should be ordered by VersionDate descending (newest first)
        Assert.Equal("Version3", versions[0].Value);
        Assert.Equal("Version2", versions[1].Value);
        Assert.Equal("Version1", versions[2].Value);
    }
}
