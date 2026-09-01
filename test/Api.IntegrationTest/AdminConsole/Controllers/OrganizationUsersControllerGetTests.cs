using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerGetTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmailA = null!;
    private Organization _organizationA = null!;

    private string _ownerEmailB = null!;
    private Organization _organizationB = null!;
    private OrganizationUser _orgUserB = null!;

    public OrganizationUsersControllerGetTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmailA = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmailA);
        (_organizationA, _) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmailA,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        _ownerEmailB = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmailB);
        (_organizationB, _orgUserB) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmailB,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_UserFromDifferentOrganization_WithSpoofedOrgIdInBody_ReturnsNotFound()
    {
        await _loginHelper.LoginAsync(_ownerEmailA);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/organizations/{_organizationA.Id}/users/{_orgUserB.Id}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["orgId"] = _organizationB.Id.ToString()
            })
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
