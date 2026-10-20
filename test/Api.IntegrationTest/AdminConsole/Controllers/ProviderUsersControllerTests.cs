using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class ProviderUsersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _providerAdminEmail = null!;
    private string _serviceUserEmail = null!;
    private string _nonMemberEmail = null!;
    private Provider _provider = null!;

    public ProviderUsersControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _providerAdminEmail = $"{Guid.NewGuid()}@test.com";
        await _factory.LoginWithNewAccount(_providerAdminEmail);

        _serviceUserEmail = $"{Guid.NewGuid()}@test.com";
        await _factory.LoginWithNewAccount(_serviceUserEmail);

        _nonMemberEmail = $"{Guid.NewGuid()}@test.com";
        await _factory.LoginWithNewAccount(_nonMemberEmail);

        var providerRepository = _factory.GetService<IProviderRepository>();
        var userRepository = _factory.GetService<IUserRepository>();
        var providerUserRepository = _factory.GetService<IProviderUserRepository>();

        _provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            BillingEmail = "billing@test.com",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Created,
            Enabled = true
        });

        var providerAdmin = await userRepository.GetByEmailAsync(_providerAdminEmail);
        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = _provider.Id,
            UserId = providerAdmin!.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
            Key = Guid.NewGuid().ToString()
        });

        var serviceUser = await userRepository.GetByEmailAsync(_serviceUserEmail);
        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = _provider.Id,
            UserId = serviceUser!.Id,
            Type = ProviderUserType.ServiceUser,
            Status = ProviderUserStatusType.Confirmed,
            Key = Guid.NewGuid().ToString()
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"providers/{_provider.Id}/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsNonMember_ReturnsForbidden()
    {
        await _loginHelper.LoginAsync(_nonMemberEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsServiceUser_ReturnsForbidden()
    {
        await _loginHelper.LoginAsync(_serviceUserEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsProviderAdmin_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Accept_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"providers/{_provider.Id}/users/{Guid.NewGuid()}/accept",
            new { Token = "invalid-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_CrossProvider_ReturnsNotFound()
    {
        var userRepository = _factory.GetService<IUserRepository>();
        var providerRepository = _factory.GetService<IProviderRepository>();
        var providerUserRepository = _factory.GetService<IProviderUserRepository>();

        var otherProviderMemberEmail = $"{Guid.NewGuid()}@test.com";
        await _factory.LoginWithNewAccount(otherProviderMemberEmail);
        var otherProviderMember = await userRepository.GetByEmailAsync(otherProviderMemberEmail);

        var otherProvider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Other Provider",
            BillingEmail = "billing@other.com",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Created,
            Enabled = true
        });

        var otherProviderUser = await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = otherProvider.Id,
            UserId = otherProviderMember!.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
            Key = Guid.NewGuid().ToString()
        });

        await _loginHelper.LoginAsync(_providerAdminEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/users/{otherProviderUser.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
