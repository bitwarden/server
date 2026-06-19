using System.Net;
using System.Net.Http.Json;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationConnectionsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationConnectionsControllerTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-connections-test-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateConnection_AsOwner_Succeeds()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.PostAsJsonAsync(
            "organizations/connections",
            BuildScimRequest(_organization.Id, enabled: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<OrganizationConnectionResponseModel>();
        Assert.NotNull(responseBody);
        Assert.Equal(OrganizationConnectionType.Scim, responseBody!.Type);
        Assert.Equal(_organization.Id, responseBody.OrganizationId);

        var connectionRepository = _factory.GetService<IOrganizationConnectionRepository>();
        var persisted = await connectionRepository.GetByOrganizationIdTypeAsync(
            _organization.Id, OrganizationConnectionType.Scim);
        var single = Assert.Single(persisted);
        Assert.True(single.GetConfig<ScimConfig>()!.Enabled);
    }

    [Fact]
    public async Task CreateConnection_AsRegularUser_Forbidden()
    {
        var (regularUserEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(regularUserEmail);

        var response = await _client.PostAsJsonAsync(
            "organizations/connections",
            BuildScimRequest(_organization.Id, enabled: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("You do not have permission to create a connection of type", body);
    }

    [Fact]
    public async Task UpdateConnection_AsOwner_Succeeds()
    {
        var existing = await SeedScimConnectionAsync(enabled: false);

        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.PutAsJsonAsync(
            $"organizations/connections/{existing.Id}",
            BuildScimRequest(_organization.Id, enabled: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var connectionRepository = _factory.GetService<IOrganizationConnectionRepository>();
        var persisted = await connectionRepository.GetByIdAsync(existing.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted!.Enabled);
        Assert.True(persisted.GetConfig<ScimConfig>()!.Enabled);
    }

    [Fact]
    public async Task UpdateConnection_AsRegularUser_Forbidden()
    {
        var existing = await SeedScimConnectionAsync();

        var (regularUserEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(regularUserEmail);

        var response = await _client.PutAsJsonAsync(
            $"organizations/connections/{existing.Id}",
            BuildScimRequest(_organization.Id, enabled: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("You do not have permission to update this connection.", body);
    }

    [Fact]
    public async Task GetConnection_AsOwner_Succeeds()
    {
        var existing = await SeedScimConnectionAsync();

        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync(
            $"organizations/connections/{_organization.Id}/{(int)OrganizationConnectionType.Scim}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<OrganizationConnectionResponseModel>();
        Assert.NotNull(responseBody);
        Assert.Equal(existing.Id, responseBody!.Id);
        Assert.Equal(OrganizationConnectionType.Scim, responseBody.Type);
    }

    [Fact]
    public async Task DeleteConnection_AsOwner_Succeeds()
    {
        var existing = await SeedScimConnectionAsync();

        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.DeleteAsync($"organizations/connections/{existing.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var connectionRepository = _factory.GetService<IOrganizationConnectionRepository>();
        var persisted = await connectionRepository.GetByIdAsync(existing.Id);
        Assert.Null(persisted);
    }

    private async Task<OrganizationConnection> SeedScimConnectionAsync(bool enabled = true)
    {
        var connectionRepository = _factory.GetService<IOrganizationConnectionRepository>();
        var connection = new OrganizationConnection
        {
            OrganizationId = _organization.Id,
            Type = OrganizationConnectionType.Scim,
            Enabled = enabled,
        };
        connection.SetConfig(new ScimConfig { Enabled = enabled });
        return await connectionRepository.CreateAsync(connection);
    }

    private static object BuildScimRequest(Guid organizationId, bool enabled) =>
        new
        {
            type = OrganizationConnectionType.Scim,
            organizationId,
            enabled,
            config = new ScimConfig { Enabled = enabled },
        };
}
