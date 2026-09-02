using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    public OrganizationUserControllerTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    [Fact]
    public async Task BulkDeleteAccount_Success()
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);

        await _loginHelper.LoginAsync(userEmail);

        var (_, orgUserToDelete) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);
        await OrganizationTestHelpers.CreateVerifiedDomainAsync(_factory, _organization.Id, "bitwarden.com");

        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        Assert.NotNull(orgUserToDelete.UserId);
        Assert.NotNull(await userRepository.GetByIdAsync(orgUserToDelete.UserId.Value));
        Assert.NotNull(await organizationUserRepository.GetByIdAsync(orgUserToDelete.Id));

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [orgUserToDelete.Id]
        };

        var httpResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/delete-account", request);
        var content = await httpResponse.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();
        Assert.Single(content.Data, r => r.Id == orgUserToDelete.Id && r.Error == string.Empty);

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Null(await userRepository.GetByIdAsync(orgUserToDelete.UserId.Value));
        Assert.Null(await organizationUserRepository.GetByIdAsync(orgUserToDelete.Id));
    }

    [Fact]
    public async Task BulkDeleteAccount_MixedResults()
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);

        await _loginHelper.LoginAsync(userEmail);

        // Can delete users
        var (_, validOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);
        // Cannot delete owners
        var (_, invalidOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.Owner);
        await OrganizationTestHelpers.CreateVerifiedDomainAsync(_factory, _organization.Id, "bitwarden.com");

        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        Assert.NotNull(validOrgUser.UserId);
        Assert.NotNull(invalidOrgUser.UserId);

        var arrangedUsers =
            await userRepository.GetManyAsync([validOrgUser.UserId.Value, invalidOrgUser.UserId.Value]);
        Assert.Equal(2, arrangedUsers.Count());

        var arrangedOrgUsers =
            await organizationUserRepository.GetManyAsync([validOrgUser.Id, invalidOrgUser.Id]);
        Assert.Equal(2, arrangedOrgUsers.Count);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [validOrgUser.Id, invalidOrgUser.Id]
        };

        var httpResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/delete-account", request);

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var debug = await httpResponse.Content.ReadAsStringAsync();
        var content = await httpResponse.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();
        Assert.Equal(2, content.Data.Count());
        Assert.Contains(content.Data, r => r.Id == validOrgUser.Id && r.Error == string.Empty);
        Assert.Contains(content.Data, r =>
            r.Id == invalidOrgUser.Id &&
            string.Equals(r.Error, new CannotDeleteOwnersError().Message, StringComparison.Ordinal));

        var actualUsers =
            await userRepository.GetManyAsync([validOrgUser.UserId.Value, invalidOrgUser.UserId.Value]);
        Assert.Single(actualUsers, u => u.Id == invalidOrgUser.UserId.Value);

        var actualOrgUsers =
            await organizationUserRepository.GetManyAsync([validOrgUser.Id, invalidOrgUser.Id]);
        Assert.Single(actualOrgUsers, ou => ou.Id == invalidOrgUser.Id);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task BulkDeleteAccount_WhenUserCannotManageUsers_ReturnsForbiddenResponse(OrganizationUserType organizationUserType)
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, organizationUserType, new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(userEmail);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = new List<Guid> { Guid.NewGuid() }
        };

        var httpResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/delete-account", request);

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_Success()
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);

        await _loginHelper.LoginAsync(userEmail);

        var (_, orgUserToDelete) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);
        await OrganizationTestHelpers.CreateVerifiedDomainAsync(_factory, _organization.Id, "bitwarden.com");

        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        Assert.NotNull(orgUserToDelete.UserId);
        Assert.NotNull(await userRepository.GetByIdAsync(orgUserToDelete.UserId.Value));
        Assert.NotNull(await organizationUserRepository.GetByIdAsync(orgUserToDelete.Id));

        var httpResponse = await _client.DeleteAsync($"organizations/{_organization.Id}/users/{orgUserToDelete.Id}/delete-account");

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.Null(await userRepository.GetByIdAsync(orgUserToDelete.UserId.Value));
        Assert.Null(await organizationUserRepository.GetByIdAsync(orgUserToDelete.Id));
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task DeleteAccount_WhenUserCannotManageUsers_ReturnsForbiddenResponse(OrganizationUserType organizationUserType)
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, organizationUserType, new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(userEmail);

        var userToRemove = Guid.NewGuid();

        var httpResponse = await _client.DeleteAsync($"organizations/{_organization.Id}/users/{userToRemove}/delete-account");

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task GetAccountRecoveryDetails_WithoutManageResetPasswordPermission_ReturnsForbiddenResponse(OrganizationUserType organizationUserType)
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, organizationUserType, new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(userEmail);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = []
        };

        var httpResponse =
            await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/account-recovery-details", request);

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-user-integration-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);
    }

    [Fact]
    public async Task Confirm_WithValidUser_ReturnsSuccess()
    {
        await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);

        var acceptedOrgUser = (await CreateAcceptedUsersAsync(new[] { ("test1@bitwarden.com", OrganizationUserType.User) })).First();

        await _loginHelper.LoginAsync(_ownerEmail);

        var confirmModel = new OrganizationUserConfirmRequestModel
        {
            Key = "test-key",
            DefaultUserCollectionName = _mockEncryptedString
        };
        var confirmResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/{acceptedOrgUser.Id}/confirm", confirmModel);

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        await VerifyUserConfirmedAsync(acceptedOrgUser, "test-key");
        await VerifyDefaultCollectionCountAsync(acceptedOrgUser, 1);
    }

    [Fact]
    public async Task Confirm_WithValidOwner_ReturnsSuccess()
    {
        await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);

        var acceptedOrgUser = (await CreateAcceptedUsersAsync(new[] { ("owner1@bitwarden.com", OrganizationUserType.Owner) })).First();

        await _loginHelper.LoginAsync(_ownerEmail);

        var confirmModel = new OrganizationUserConfirmRequestModel
        {
            Key = "test-key",
            DefaultUserCollectionName = _mockEncryptedString
        };
        var confirmResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/{acceptedOrgUser.Id}/confirm", confirmModel);

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        await VerifyUserConfirmedAsync(acceptedOrgUser, "test-key");
        await VerifyDefaultCollectionCountAsync(acceptedOrgUser, 0);
    }

    [Fact]
    public async Task BulkConfirm_WithValidUsers_ReturnsSuccess()
    {
        const string testKeyFormat = "test-key-{0}";
        await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);

        var acceptedUsers = await CreateAcceptedUsersAsync([
            ("test1@example.com", OrganizationUserType.User),
            ("test2@example.com", OrganizationUserType.Owner),
            ("test3@example.com", OrganizationUserType.User)
        ]);

        await _loginHelper.LoginAsync(_ownerEmail);

        var bulkConfirmModel = new OrganizationUserBulkConfirmRequestModel
        {
            Keys = acceptedUsers.Select((organizationUser, index) => new OrganizationUserBulkConfirmRequestModelEntry
            {
                Id = organizationUser.Id,
                Key = string.Format(testKeyFormat, index)
            }),
            DefaultUserCollectionName = _mockEncryptedString
        };

        var bulkConfirmResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/confirm", bulkConfirmModel);

        Assert.Equal(HttpStatusCode.OK, bulkConfirmResponse.StatusCode);

        await VerifyMultipleUsersConfirmedAsync(acceptedUsers.Select((organizationUser, index) =>
            (organizationUser, string.Format(testKeyFormat, index))).ToList());
        await VerifyDefaultCollectionCountAsync(acceptedUsers.ElementAt(0), 1);
        await VerifyDefaultCollectionCountAsync(acceptedUsers.ElementAt(1), 0); // Owner does not get a default collection
        await VerifyDefaultCollectionCountAsync(acceptedUsers.ElementAt(2), 1);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<List<OrganizationUser>> CreateAcceptedUsersAsync(
        IEnumerable<(string email, OrganizationUserType userType)> newUsers)
    {
        var acceptedUsers = new List<OrganizationUser>();

        foreach (var (email, userType) in newUsers)
        {
            await _factory.LoginWithNewAccount(email);

            var acceptedOrgUser = await OrganizationTestHelpers.CreateUserAsync(
                _factory, _organization.Id, email,
                userType, userStatusType: OrganizationUserStatusType.Accepted);

            acceptedUsers.Add(acceptedOrgUser);
        }

        return acceptedUsers;
    }

    private async Task VerifyDefaultCollectionCountAsync(OrganizationUser orgUser, int expectedCount)
    {
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collections = await collectionRepository.GetManyByUserIdAsync(orgUser.UserId!.Value);
        Assert.Equal(expectedCount, collections.Count);
    }

    private async Task VerifyUserConfirmedAsync(OrganizationUser orgUser, string expectedKey)
    {
        await VerifyMultipleUsersConfirmedAsync(new List<(OrganizationUser orgUser, string key)> { (orgUser, expectedKey) });
    }

    private async Task VerifyMultipleUsersConfirmedAsync(List<(OrganizationUser orgUser, string key)> acceptedOrganizationUsers)
    {
        var orgUserRepository = _factory.GetService<IOrganizationUserRepository>();
        for (int i = 0; i < acceptedOrganizationUsers.Count; i++)
        {
            var confirmedUser = await orgUserRepository.GetByIdAsync(acceptedOrganizationUsers[i].orgUser.Id);
            Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser.Status);
            Assert.Equal(acceptedOrganizationUsers[i].key, confirmedUser.Key);
        }
    }
}
