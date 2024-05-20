using System.Net;
using Bit.Api.AdminConsole.Public.Models;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Public.Controllers;

public class MembersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private Organization _organization;

    public MembersControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        // Create the owner account
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        // Create the organization
        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        // Authorize with the organization api key
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_CustomUser_Success()
    {
        var (email, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Custom, new Permissions { AccessReports = true, ManageScim = true });

        var response = await _client.GetAsync($"/public/members/{orgUser.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MemberResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);

        AssertPermissionsEqual(new PermissionsModel { AccessReports = true, ManageScim = true },
            result.Permissions);
    }

    private void AssertPermissionsEqual(PermissionsModel expected, PermissionsModel actual)
    {
        AssertHelper.AssertPropertyEqual(expected, actual,
            [
                "EditAssignedCollections", // Deprecated and not included in the Public API
                "DeleteAssignedCollections", // Deprecated and not included in the Public API
                "ClaimsMap" // internal
            ]);
    }
}
