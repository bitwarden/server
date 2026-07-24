using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerGetMiniDetailsTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

    public OrganizationUsersControllerGetMiniDetailsTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetMiniDetails_AsOwner_ReturnsSuccess()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/users/mini-details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMiniDetails_AsUser_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/users/mini-details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMiniDetails_AsProviderUser_ReturnsSuccess()
    {
        var email = await CreateProviderUserForOrganizationAsync();
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/users/mini-details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMiniDetails_NotAMember_ReturnsForbidden()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/users/mini-details");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Creates a provider linked to the test organization and returns a provider user's credentials.
    /// </summary>
    private async Task<string> CreateProviderUserForOrganizationAsync()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);

        var provider = await ProviderTestHelpers.CreateProviderAndLinkToOrganizationAsync(
            _factory, _organization.Id, ProviderType.Msp);

        await ProviderTestHelpers.CreateProviderUserAsync(
            _factory, provider.Id, email, ProviderUserType.ServiceUser);

        return email;
    }
}
