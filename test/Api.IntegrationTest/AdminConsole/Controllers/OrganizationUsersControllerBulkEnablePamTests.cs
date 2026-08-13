using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;
using V1_RestoreUserCommand = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerBulkEnablePamTests
    : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerBulkEnablePamTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"bulk-enable-pam-{Guid.NewGuid()}@bitwarden.com";
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
    public async Task BulkEnablePam_GrantsAccessAndLeavesMembersThatAlreadyHaveItUntouched()
    {
        await SetUsePamAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var member = await CreateMemberAsync();
        var alreadyEnabledMember = await CreateMemberAsync();
        await GrantPamAsync(alreadyEnabledMember);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/enable-pam",
            new OrganizationUserBulkRequestModel { Ids = [member.Id, alreadyEnabledMember.Id] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAccessPamAsync(member, true);
        await AssertAccessPamAsync(alreadyEnabledMember, true);
    }

    [Fact]
    public async Task BulkEnablePam_WhenOrganizationDoesNotUsePam_ReturnsBadRequestAndPersistsNothing()
    {
        await SetUsePamAsync(false);
        await _loginHelper.LoginAsync(_ownerEmail);

        var member = await CreateMemberAsync();

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/enable-pam",
            new OrganizationUserBulkRequestModel { Ids = [member.Id] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new PamNotEnabled().Message, await response.Content.ReadAsStringAsync());
        await AssertAccessPamAsync(member, false);
    }

    [Fact]
    public async Task BulkEnablePam_WhenEveryMemberAlreadyHasAccess_ReturnsBadRequest()
    {
        await SetUsePamAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var member = await CreateMemberAsync();
        await GrantPamAsync(member);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/enable-pam",
            new OrganizationUserBulkRequestModel { Ids = [member.Id] });

        // The empty-set check runs before the UsePam check, so this is "Users invalid." rather than the PAM error.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new V1_RestoreUserCommand.UsersInvalid().Message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BulkEnablePam_DoesNotGrantAccessToMembersOfAnotherOrganization()
    {
        await SetUsePamAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var member = await CreateMemberAsync();

        // A second organization owned by the same account, so the request is authorized for the first one only.
        var (otherOrganization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _ownerEmail, passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);
        var otherOrgMember = await CreateMemberAsync(otherOrganization.Id);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/enable-pam",
            new OrganizationUserBulkRequestModel { Ids = [member.Id, otherOrgMember.Id] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAccessPamAsync(member, true);
        await AssertAccessPamAsync(otherOrgMember, false);
    }

    [Fact]
    public async Task BulkEnablePam_GrantsAccessToInvitedAndRevokedMembers()
    {
        await SetUsePamAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        // The endpoint filters on AccessPam only, so members who cannot currently use the access are still granted it.
        var invitedMember = await CreateMemberAsync(status: OrganizationUserStatusType.Invited);
        var revokedMember = await CreateMemberAsync();
        await _factory.GetService<IOrganizationUserRepository>()
            .RevokeAsync(revokedMember.Id, RevocationReason.Manual);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/enable-pam",
            new OrganizationUserBulkRequestModel { Ids = [invitedMember.Id, revokedMember.Id] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAccessPamAsync(invitedMember, true);
        await AssertAccessPamAsync(revokedMember, true);
    }

    [Fact]
    public async Task BulkEnablePam_WithUnknownIds_ReturnsBadRequest()
    {
        await SetUsePamAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/enable-pam",
            new OrganizationUserBulkRequestModel { Ids = [Guid.NewGuid()] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new V1_RestoreUserCommand.UsersInvalid().Message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BulkEnablePam_WithEmptyIds_ReturnsModelStateBadRequest()
    {
        await SetUsePamAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/enable-pam",
            new OrganizationUserBulkRequestModel { Ids = [] });

        // Ids is [Required, MinLength(1)], so this is rejected by model validation before the endpoint runs.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("model state is invalid", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task BulkEnablePam_WithoutManageUsersPermission_ReturnsForbidden(OrganizationUserType userType)
    {
        await SetUsePamAsync(true);

        var (callerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, userType, new Permissions { ManageUsers = false });
        await _loginHelper.LoginAsync(callerEmail);

        var member = await CreateMemberAsync();

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/enable-pam",
            new OrganizationUserBulkRequestModel { Ids = [member.Id] });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertAccessPamAsync(member, false);
    }

    private async Task<OrganizationUser> CreateMemberAsync(Guid? organizationId = null,
        OrganizationUserStatusType status = OrganizationUserStatusType.Confirmed)
    {
        var email = $"bulk-enable-pam-member-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        return await OrganizationTestHelpers.CreateUserAsync(_factory, organizationId ?? _organization.Id, email,
            OrganizationUserType.User, userStatusType: status);
    }

    private async Task SetUsePamAsync(bool value)
    {
        _organization.UsePam = value;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);
    }

    private async Task GrantPamAsync(OrganizationUser organizationUser)
    {
        organizationUser.AccessPam = true;
        await _factory.GetService<IOrganizationUserRepository>().ReplaceAsync(organizationUser);
    }

    private async Task AssertAccessPamAsync(OrganizationUser organizationUser, bool expected)
    {
        var reloaded = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(expected, reloaded.AccessPam);
    }
}
