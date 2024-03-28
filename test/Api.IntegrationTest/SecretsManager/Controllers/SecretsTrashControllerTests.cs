using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Repositories;
using Xunit;
using Secret = Bit.Core.SecretsManager.Entities.Secret;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class SecretsTrashControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ISecretRepository _secretRepository;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public SecretsTrashControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _secretRepository = _factory.GetService<ISecretRepository>();
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
    public async Task ListByOrganization_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"/secrets/{org.Id}/trash");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListByOrganization_NotAdmin_Unauthorized()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/secrets/{org.Id}/trash");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListByOrganization_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            DeletedDate = DateTime.Now,
        });

        await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/secrets/{org.Id}/trash");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretWithProjectsListResponseModel>();
        Assert.Single(result!.Secrets);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Empty_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var ids = new List<Guid> { Guid.NewGuid() };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/empty", ids);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Empty_NotAdmin_Unauthorized()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var ids = new List<Guid> { Guid.NewGuid() };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/empty", ids);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Empty_Invalid_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString
        });

        var ids = new List<Guid> { secret.Id };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/empty", ids);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Empty_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            DeletedDate = DateTime.Now,
        });

        var ids = new List<Guid> { secret.Id };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/empty", ids);
        response.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Restore_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var ids = new List<Guid> { Guid.NewGuid() };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/restore", ids);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Restore_NotAdmin_Unauthorized()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var ids = new List<Guid> { Guid.NewGuid() };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/restore", ids);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Restore_Invalid_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString
        });

        var ids = new List<Guid> { secret.Id };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/restore", ids);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Restore_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            DeletedDate = DateTime.Now,
        });

        var ids = new List<Guid> { secret.Id };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/restore", ids);
        response.EnsureSuccessStatusCode();
    }
}
