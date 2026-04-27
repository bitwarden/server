using System.Net;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class GroupsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;
    private Group _group = null!;

    public GroupsControllerTests(ApiApplicationFactory factory)
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

        _group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_AsOwner_ReturnsSuccess()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsAdmin_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Admin);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsCustomWithManageGroups_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageGroups = true });
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsCustomWithoutManageGroups_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageGroups = false });
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsUser_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsProviderUser_ReturnsSuccess()
    {
        var email = await CreateProviderUserForOrganizationAsync();
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_NotAMember_ReturnsForbidden()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroups_AsOwner_ReturnsSuccess()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroups_AsUser_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroups_AsProviderUser_ReturnsSuccess()
    {
        var email = await CreateProviderUserForOrganizationAsync();
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroups_NotAMember_ReturnsForbidden()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroupDetails_AsOwner_ReturnsSuccess()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroupDetails_AsAdmin_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Admin);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroupDetails_AsCustomWithManageGroups_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageGroups = true });
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroupDetails_AsCustomWithManageUsers_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageUsers = true });
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroupDetails_AsCustomWithoutManageUsersOrGroups_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageUsers = false, ManageGroups = false });
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/details");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroupDetails_AsUser_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/details");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationGroupDetails_AsProviderUser_ReturnsSuccess()
    {
        var email = await CreateProviderUserForOrganizationAsync();
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsOwner_ReturnsSuccess()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsUser_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsCustomWithManageGroups_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageGroups = true });
        await _loginHelper.LoginAsync(email);

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/groups/{_group.Id}/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_AsOwner_ReturnsSuccess()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var request = new GroupRequestModel { Name = "New Group" };
        var response = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/groups", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_AsAdmin_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Admin);
        await _loginHelper.LoginAsync(email);

        var request = new GroupRequestModel { Name = "New Group" };
        var response = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/groups", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_AsUser_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var request = new GroupRequestModel { Name = "New Group" };
        var response = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/groups", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_AsCustomWithManageGroups_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageGroups = true });
        await _loginHelper.LoginAsync(email);

        var request = new GroupRequestModel { Name = "New Group" };
        var response = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/groups", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_AsCustomWithoutManageGroups_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageGroups = false });
        await _loginHelper.LoginAsync(email);

        var request = new GroupRequestModel { Name = "New Group" };
        var response = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/groups", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsOwner_ReturnsSuccess()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);

        var response = await _client.DeleteAsync($"/organizations/{_organization.Id}/groups/{group.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsUser_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var response = await _client.DeleteAsync($"/organizations/{_organization.Id}/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsCustomWithManageGroups_ReturnsSuccess()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.Custom,
            permissions: new Permissions { ManageGroups = true });
        await _loginHelper.LoginAsync(email);
        var group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);

        var response = await _client.DeleteAsync($"/organizations/{_organization.Id}/groups/{group.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_AsOwner_IsAuthorized()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);

        var request = new GroupBulkRequestModel { Ids = [group.Id] };
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/organizations/{_organization.Id}/groups")
        {
            Content = JsonContent.Create(request)
        });

        // Assert authorization passes (not Forbidden/Unauthorized)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_AsUser_ReturnsForbidden()
    {
        var (email, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var request = new GroupBulkRequestModel { Ids = [_group.Id] };
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/organizations/{_organization.Id}/groups")
        {
            Content = JsonContent.Create(request)
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsUser_ReturnsForbidden()
    {
        var (email, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(email);

        var response = await _client.DeleteAsync(
            $"/organizations/{_organization.Id}/groups/{_group.Id}/user/{orgUser.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsOwner_GroupNotFound_ReturnsNotFound()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.DeleteAsync(
            $"/organizations/{_organization.Id}/groups/{Guid.NewGuid()}/user/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
