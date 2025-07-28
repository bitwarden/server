using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerTests(ApiApplicationFactory apiApplicationFactory) : IntegrationTestBase(apiApplicationFactory)
{
    private LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task BulkDeleteAccount_WhenUserCannotManageUsers_ReturnsForbiddenResponse(OrganizationUserType organizationUserType)
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            _organization.Id, organizationUserType, new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(userEmail);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = new List<Guid> { Guid.NewGuid() }
        };

        var httpResponse = await Client.PostAsJsonAsync($"organizations/{_organization.Id}/users/remove", request);

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task DeleteAccount_WhenUserCannotManageUsers_ReturnsForbiddenResponse(OrganizationUserType organizationUserType)
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            _organization.Id, organizationUserType, new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(userEmail);

        var userToRemove = Guid.NewGuid();

        var httpResponse = await Client.DeleteAsync($"organizations/{_organization.Id}/users/{userToRemove}");

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task GetAccountRecoveryDetails_WithoutManageResetPasswordPermission_ReturnsForbiddenResponse(OrganizationUserType organizationUserType)
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(Factory,
            _organization.Id, organizationUserType, new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(userEmail);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = []
        };

        var httpResponse =
            await Client.PostAsJsonAsync($"organizations/{_organization.Id}/users/account-recovery-details", request);

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _loginHelper = new LoginHelper(Factory, Client);

        _ownerEmail = $"org-user-integration-test-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(Factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);
    }
}
