using System.Net;
using Bit.Api.Billing.Models.Requests;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class ProviderClientsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _providerAdminEmail = null!;
    private string _serviceUserEmail = null!;
    private string _otherUserEmail = null!;
    private Provider _provider = null!;
    private Organization _addableOrg = null!;
    private Organization _managedOrg = null!;
    private Organization _otherOrg = null!;

    public ProviderClientsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IProviderBillingService>(_ => { });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _providerAdminEmail = $"{Guid.NewGuid()}@test.com";
        await _factory.LoginWithNewAccount(_providerAdminEmail);

        _serviceUserEmail = $"{Guid.NewGuid()}@test.com";
        await _factory.LoginWithNewAccount(_serviceUserEmail);

        _otherUserEmail = $"{Guid.NewGuid()}@test.com";
        await _factory.LoginWithNewAccount(_otherUserEmail);

        var userRepository = _factory.GetService<IUserRepository>();
        var orgRepository = _factory.GetService<IOrganizationRepository>();
        var orgUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var providerRepository = _factory.GetService<IProviderRepository>();
        var providerUserRepository = _factory.GetService<IProviderUserRepository>();

        var providerAdmin = await userRepository.GetByEmailAsync(_providerAdminEmail);
        var serviceUser = await userRepository.GetByEmailAsync(_serviceUserEmail);
        var otherUser = await userRepository.GetByEmailAsync(_otherUserEmail);

        _provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test MSP Provider",
            BillingEmail = "billing@test.com",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Billable,
            Enabled = true,
            GatewayCustomerId = $"cus_{Guid.NewGuid():N}",
            GatewaySubscriptionId = $"sub_{Guid.NewGuid():N}"
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = _provider.Id,
            UserId = providerAdmin!.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Confirmed,
            Key = Guid.NewGuid().ToString()
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = _provider.Id,
            UserId = serviceUser!.Id,
            Type = ProviderUserType.ServiceUser,
            Status = ProviderUserStatusType.Confirmed,
            Key = Guid.NewGuid().ToString()
        });

        _addableOrg = await orgRepository.CreateAsync(new Organization
        {
            Name = "Addable Org",
            BillingEmail = _providerAdminEmail,
            Plan = "Teams (Monthly)",
            PlanType = PlanType.TeamsMonthly,
            Status = OrganizationStatusType.Created,
            Enabled = true,
            Seats = 10,
            GatewayCustomerId = $"cus_{Guid.NewGuid():N}",
            GatewaySubscriptionId = $"sub_{Guid.NewGuid():N}",
            UseSecretsManager = false
        });

        await orgUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = _addableOrg.Id,
            UserId = providerAdmin.Id,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Confirmed,
            AccessSecretsManager = false
        });

        _managedOrg = await orgRepository.CreateAsync(new Organization
        {
            Name = "Managed Org",
            BillingEmail = _providerAdminEmail,
            Plan = "Teams (Monthly)",
            PlanType = PlanType.TeamsMonthly,
            Status = OrganizationStatusType.Managed,
            Enabled = true,
            Seats = 10,
            GatewayCustomerId = $"cus_{Guid.NewGuid():N}",
            GatewaySubscriptionId = $"sub_{Guid.NewGuid():N}",
            UseSecretsManager = false
        });

        await orgUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = _managedOrg.Id,
            UserId = providerAdmin.Id,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Confirmed,
            AccessSecretsManager = false
        });

        _otherOrg = await orgRepository.CreateAsync(new Organization
        {
            Name = "Other User Org",
            BillingEmail = _otherUserEmail,
            Plan = "Teams (Monthly)",
            PlanType = PlanType.TeamsMonthly,
            Status = OrganizationStatusType.Created,
            Enabled = true,
            Seats = 10,
            GatewayCustomerId = $"cus_{Guid.NewGuid():N}",
            GatewaySubscriptionId = $"sub_{Guid.NewGuid():N}",
            UseSecretsManager = false
        });

        await orgUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = _otherOrg.Id,
            UserId = otherUser!.Id,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Confirmed,
            AccessSecretsManager = false
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddExistingOrganizationAsync_Unauthenticated_ReturnsUnauthorized()
    {
        var request = new AddExistingOrganizationRequestBody { OrganizationId = _addableOrg.Id, Key = "key" };

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/clients/existing", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddExistingOrganizationAsync_AsServiceUser_ReturnsForbidden()
    {
        await _loginHelper.LoginAsync(_serviceUserEmail);

        var request = new AddExistingOrganizationRequestBody { OrganizationId = _addableOrg.Id, Key = "key" };

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/clients/existing", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddExistingOrganizationAsync_NotOrgOwner_ReturnsUnauthorized()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);

        var request = new AddExistingOrganizationRequestBody { OrganizationId = _otherOrg.Id, Key = "key" };

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/clients/existing", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddExistingOrganizationAsync_OrgNotAddable_ReturnsNotFound()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);

        var request = new AddExistingOrganizationRequestBody { OrganizationId = _managedOrg.Id, Key = "key" };

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/clients/existing", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddExistingOrganizationAsync_ValidRequest_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);

        var request = new AddExistingOrganizationRequestBody { OrganizationId = _addableOrg.Id, Key = "key" };

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/clients/existing", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var billingService = _factory.GetService<IProviderBillingService>();
        await billingService.Received(1).AddExistingOrganization(
            Arg.Is<Provider>(p => p.Id == _provider.Id),
            Arg.Is<Organization>(o => o.Id == _addableOrg.Id),
            "key");
    }

    [Fact]
    public async Task GetAddableOrganizations_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"providers/{_provider.Id}/clients/addable");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAddableOrganizations_AsNonMember_ReturnsForbidden()
    {
        await _loginHelper.LoginAsync(_otherUserEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/clients/addable");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAddableOrganizations_AsServiceUser_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_serviceUserEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/clients/addable");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAddableOrganizations_AsProviderAdmin_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/clients/addable");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
