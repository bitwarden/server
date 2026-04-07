using System.Net;
using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class ProviderOrganizationsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _providerAdminEmail = null!;
    private string _serviceUserEmail = null!;
    private string _otherUserEmail = null!;
    private Provider _provider = null!;
    private Organization _org = null!;
    private Organization _otherOrg = null!;

    public ProviderOrganizationsControllerTests(ApiApplicationFactory factory)
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
            Name = "Test Provider",
            BillingEmail = "billing@test.com",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Created,
            Enabled = true
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

        _org = await orgRepository.CreateAsync(new Organization
        {
            Name = "Provider Admin Org",
            BillingEmail = _providerAdminEmail,
            Plan = "Teams (Monthly)",
            PlanType = PlanType.TeamsMonthly,
            Status = OrganizationStatusType.Created,
            Enabled = true,
            Seats = 10,
        });

        await orgUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = _org.Id,
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
    public async Task Add_Unauthenticated_ReturnsUnauthorized()
    {
        var model = new ProviderOrganizationAddRequestModel { OrganizationId = _org.Id, Key = "key" };

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/organizations/add", model);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Add_NotOrgOwner_ReturnsNotFound()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);

        var model = new ProviderOrganizationAddRequestModel { OrganizationId = _otherOrg.Id, Key = "key" };

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/organizations/add", model);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Add_ValidRequest_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);

        var providerOrganizationRepository = _factory.GetService<IProviderOrganizationRepository>();

        var model = new ProviderOrganizationAddRequestModel { OrganizationId = _org.Id, Key = "key" };

        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/organizations/add", model);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var providerOrganization = await providerOrganizationRepository.GetByOrganizationId(_org.Id);
        Assert.NotNull(providerOrganization);
        Assert.Equal(_provider.Id, providerOrganization.ProviderId);
    }

    // GET /providers/{providerId}/organizations — ProviderUserRequirement
    // Both ProviderAdmin and ServiceUser should be allowed; non-members should be rejected.

    [Fact]
    public async Task Get_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"providers/{_provider.Id}/organizations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsNonMember_ReturnsForbidden()
    {
        await _loginHelper.LoginAsync(_otherUserEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/organizations");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsServiceUser_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_serviceUserEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/organizations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsProviderAdmin_ReturnsOk()
    {
        await _loginHelper.LoginAsync(_providerAdminEmail);
        var response = await _client.GetAsync($"providers/{_provider.Id}/organizations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // POST /providers/{providerId}/organizations/add — ProviderAdminRequirement
    // Only ProviderAdmin should be allowed; ServiceUser should be rejected.

    [Fact]
    public async Task Add_AsNonMember_ReturnsForbidden()
    {
        await _loginHelper.LoginAsync(_otherUserEmail);
        var model = new ProviderOrganizationAddRequestModel { OrganizationId = _org.Id, Key = "key" };
        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/organizations/add", model);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Add_AsServiceUser_ReturnsForbidden()
    {
        await _loginHelper.LoginAsync(_serviceUserEmail);
        var model = new ProviderOrganizationAddRequestModel { OrganizationId = _org.Id, Key = "key" };
        var response = await _client.PostAsJsonAsync($"providers/{_provider.Id}/organizations/add", model);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
