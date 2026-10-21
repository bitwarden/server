using System.Net;
using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class ProvidersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _providerAdminEmail = null!;
    private string _serviceUserEmail = null!;
    private string _nonMemberEmail = null!;
    private Provider _provider = null!;

    public ProvidersControllerTests(ApiApplicationFactory factory)
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
    public async Task Get_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"providers/{_provider.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsNonMember_ReturnsForbidden()
    {
        await _loginHelper.LoginAsync(_nonMemberEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsServiceUser_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_serviceUserEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsProviderAdmin_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostDeleteRecoverToken_ValidToken_DeletesProvider()
    {
        var token = _factory.GetService<IDataProtectorTokenFactory<ProviderDeleteTokenable>>()
            .Protect(new ProviderDeleteTokenable(_provider));

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/delete-recover-token",
            new ProviderVerifyDeleteRecoverRequestModel { Token = token });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var providerRepository = _factory.GetService<IProviderRepository>();
        Assert.Null(await providerRepository.GetByIdAsync(_provider.Id));
    }

    [Fact]
    public async Task PostDeleteRecoverToken_ExpiredToken_ReturnsBadRequestAndDoesNotDeleteProvider()
    {
        var token = _factory.GetService<IDataProtectorTokenFactory<ProviderDeleteTokenable>>()
            .Protect(new ProviderDeleteTokenable { Id = _provider.Id, ExpirationDate = DateTime.UtcNow.AddHours(-1) });

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/delete-recover-token",
            new ProviderVerifyDeleteRecoverRequestModel { Token = token });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var providerRepository = _factory.GetService<IProviderRepository>();
        Assert.NotNull(await providerRepository.GetByIdAsync(_provider.Id));
    }
}
