using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Authorization;

public class OrganizationUsersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    // These will get set in `InitializeAsync` which is run before all tests
    private Organization _organization = null!;
    private OrganizationUser _organizationUser = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        // Create the owner account
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        // Create the organization
        (_organization, _organizationUser) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: _ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        // Login as the user
        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetMiniDetails_Authorization_Fail()
    {
        // Request is for a random organizationId not in their claims
        var organizationId = Guid.NewGuid();
        var response = await _client.GetAsync($"/organizations/{organizationId}/users/mini-details");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMiniDetails_Authorization_Success()
    {
        // Request is for their organization
        var response = await _client.GetAsync($"/organizations/{_organization.Id}/users/mini-details");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
