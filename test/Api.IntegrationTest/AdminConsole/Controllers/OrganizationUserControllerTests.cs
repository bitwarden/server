using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";


    public OrganizationUserControllerTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
                .Returns(true);
        });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

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

        var httpResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/remove", request);

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
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

        var httpResponse = await _client.DeleteAsync($"organizations/{_organization.Id}/users/{userToRemove}");

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

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);
    }

    [Fact]
    public async Task Confirm_WithValidUser_ReturnsSuccess()
    {
        await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);

        var acceptedOrgUser = (await CreateAcceptedUsersAsync(new[] { "test1@bitwarden.com" })).First();

        await _loginHelper.LoginAsync(_ownerEmail);

        var confirmModel = new OrganizationUserConfirmRequestModel
        {
            Key = "test-key",
            DefaultUserCollectionName = _mockEncryptedString
        };
        var confirmResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/{acceptedOrgUser.Id}/confirm", confirmModel);

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        await VerifyUserConfirmedAsync(acceptedOrgUser, "test-key");
        await VerifyDefaultCollectionCreatedAsync(acceptedOrgUser);
    }

    [Fact]
    public async Task BulkConfirm_WithValidUsers_ReturnsSuccess()
    {
        const string testKeyFormat = "test-key-{0}";
        await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);

        var emails = new[] { "test1@example.com", "test2@example.com", "test3@example.com" };
        var acceptedUsers = await CreateAcceptedUsersAsync(emails);

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
        await VerifyMultipleUsersHaveDefaultCollectionsAsync(acceptedUsers);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<List<OrganizationUser>> CreateAcceptedUsersAsync(IEnumerable<string> emails)
    {
        var acceptedUsers = new List<OrganizationUser>();

        foreach (var email in emails)
        {
            await _factory.LoginWithNewAccount(email);

            var acceptedOrgUser = await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email,
                OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Accepted);

            acceptedUsers.Add(acceptedOrgUser);
        }

        return acceptedUsers;
    }

    private async Task VerifyDefaultCollectionCreatedAsync(OrganizationUser orgUser)
    {
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collections = await collectionRepository.GetManyByUserIdAsync(orgUser.UserId!.Value);
        Assert.Single(collections);
        Assert.Equal(_mockEncryptedString, collections.First().Name);
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

    private async Task VerifyMultipleUsersHaveDefaultCollectionsAsync(List<OrganizationUser> acceptedOrganizationUsers)
    {
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        foreach (var acceptedOrganizationUser in acceptedOrganizationUsers)
        {
            var collections = await collectionRepository.GetManyByUserIdAsync(acceptedOrganizationUser.UserId!.Value);
            Assert.Single(collections);
            Assert.Equal(_mockEncryptedString, collections.First().Name);
        }
    }
}
