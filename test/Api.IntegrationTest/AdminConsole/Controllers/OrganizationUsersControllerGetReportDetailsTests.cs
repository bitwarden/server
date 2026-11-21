using System.Net;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.HttpExtensions;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerGetReportDetailsTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

    public OrganizationUsersControllerGetReportDetailsTests(ApiApplicationFactory factory)
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

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task GetReportDetails_AsMemberWhoCanAccessReports_ReturnsEveryMember(OrganizationUserType type)
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        var (callerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, type,
            type == OrganizationUserType.Custom ? new Permissions { AccessReports = true } : null);
        await _loginHelper.LoginAsync(callerEmail);

        var response = await _client.GetAsync(ReportDetailsUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var emails = await ReadEmailsAsync(response);
        Assert.Contains(_ownerEmail, emails);
        Assert.Contains(memberEmail, emails);
        Assert.Contains(callerEmail, emails);
    }

    [Theory]
    [InlineData(OrganizationUserType.User, false, false)]
    [InlineData(OrganizationUserType.Custom, true, false)]
    [InlineData(OrganizationUserType.Custom, false, true)]
    public async Task GetReportDetails_AsMemberWithoutAccessReports_ReturnsForbidden(
        OrganizationUserType type, bool manageUsers, bool manageResetPassword)
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, type,
            type == OrganizationUserType.Custom
                ? new Permissions { ManageUsers = manageUsers, ManageResetPassword = manageResetPassword }
                : null);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync(ReportDetailsUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetReportDetails_AsProviderUser_ReturnsEveryMember()
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        var providerUserEmail = await CreateProviderUserForOrganizationAsync();
        await _loginHelper.LoginAsync(providerUserEmail);

        var response = await _client.GetAsync(ReportDetailsUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var emails = await ReadEmailsAsync(response);
        Assert.Contains(_ownerEmail, emails);
        Assert.Contains(memberEmail, emails);
    }

    [Fact]
    public async Task GetReportDetails_NotAMember_ReturnsForbidden()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync(ReportDetailsUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private string ReportDetailsUrl => $"/organizations/{_organization.Id}/users/report-details";

    private static async Task<List<string>> ReadEmailsAsync(HttpResponseMessage response)
    {
        var body = await response.Content
            .ReadFromJsonAsync<ListResponseModel<OrganizationUserReportDetailsResponseModel>>();
        Assert.NotNull(body);
        return body.Data.Select(member => member.Email).ToList();
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
