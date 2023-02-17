using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Repositories;
using Xunit;
using Secret = Bit.Core.SecretsManager.Entities.Secret;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class SecretsTrashControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ISecretRepository _secretRepository;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public SecretsTrashControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _secretRepository = _factory.GetService<ISecretRepository>();
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

    private async Task LoginAsync(string email)
    {
        var tokens = await _factory.LoginAsync(email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Restore_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var ids = new List<Guid> { Guid.NewGuid() };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/restore", ids);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Restore_NotAdmin_Unauthorized()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var ids = new List<Guid> { Guid.NewGuid() };
        var response = await _client.PostAsJsonAsync($"/secrets/{org.Id}/trash/restore", ids);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Restore_Invalid_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

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
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

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
