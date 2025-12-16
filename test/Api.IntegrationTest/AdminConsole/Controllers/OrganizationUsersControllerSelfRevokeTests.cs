using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerSelfRevokeTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;

    public OrganizationUsersControllerSelfRevokeTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SelfRevoke_WhenPolicyEnabledAndUserIsEligible_ReturnsOk()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        var policy = new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true,
            Data = null
        };
        await _factory.GetService<IPolicyRepository>().CreateAsync(policy);

        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory,
            organization.Id,
            OrganizationUserType.User);
        await _loginHelper.LoginAsync(userEmail);

        var result = await _client.PutAsync($"organizations/{organization.Id}/users/revoke-self", null);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var organizationUsers = await organizationUserRepository.GetManyDetailsByOrganizationAsync(organization.Id);
        var revokedUser = organizationUsers.FirstOrDefault(u => u.Email == userEmail);

        Assert.NotNull(revokedUser);
        Assert.Equal(OrganizationUserStatusType.Revoked, revokedUser.Status);
    }

    [Fact]
    public async Task SelfRevoke_WhenUserNotMemberOfOrganization_ReturnsForbidden()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        var policy = new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true,
            Data = null
        };
        await _factory.GetService<IPolicyRepository>().CreateAsync(policy);

        var nonMemberEmail = $"{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(nonMemberEmail);
        await _loginHelper.LoginAsync(nonMemberEmail);

        var result = await _client.PutAsync($"organizations/{organization.Id}/users/revoke-self", null);

        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public async Task SelfRevoke_WhenUserIsOwnerOrAdmin_ReturnsBadRequest(OrganizationUserType userType)
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        var policy = new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true,
            Data = null
        };
        await _factory.GetService<IPolicyRepository>().CreateAsync(policy);

        string userEmail;
        if (userType == OrganizationUserType.Owner)
        {
            userEmail = _ownerEmail;
        }
        else
        {
            (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
                _factory,
                organization.Id,
                userType);
        }

        await _loginHelper.LoginAsync(userEmail);

        var result = await _client.PutAsync($"organizations/{organization.Id}/users/revoke-self", null);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
}
