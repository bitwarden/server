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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirm_WithValidUser_ReturnsSuccess(bool enableOrganizationDataOwnershipPolicy)
    {
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
                .Returns(_ => true);
        });

        // Enable the Organization Data Ownership policy to trigger the DefaultUserCollection creation
        if (enableOrganizationDataOwnershipPolicy)
        {
            await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);
        }

        var acceptedUserEmail = $"{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(acceptedUserEmail);

        var acceptedOrgUser = await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, acceptedUserEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Accepted);

        // Login as owner for confirmation
        await _loginHelper.LoginAsync(_ownerEmail);

        // Confirm the user via the controller endpoint
        var confirmModel = new OrganizationUserConfirmRequestModel
        {
            Key = "test-key",
            DefaultUserCollectionName = _mockEncryptedString
        };
        var confirmResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/{acceptedOrgUser.Id}/confirm", confirmModel);

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        // Verify user status changed to confirmed
        var orgUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var confirmedUser = await orgUserRepository.GetByIdAsync(acceptedOrgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser.Status);
        Assert.Equal("test-key", confirmedUser.Key);

        if (enableOrganizationDataOwnershipPolicy)
        {
            // Verify user has a DefaultUserCollection
            var collectionRepository = _factory.GetService<ICollectionRepository>();
            var collections = await collectionRepository.GetManyByUserIdAsync(acceptedOrgUser.UserId!.Value);
            Assert.Single(collections);
            Assert.Equal(_mockEncryptedString, collections.First().Name);
        }
        else
        {
            var collectionRepository = _factory.GetService<ICollectionRepository>();
            var organizationCollectionCount = await collectionRepository.GetCountByOrganizationIdAsync(_organization.Id);
            Assert.Equal(0, organizationCollectionCount);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BulkConfirm_WithValidUsers_ReturnsSuccess(bool enableOrganizationDataOwnershipPolicy)
    {
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
                .Returns(_ => true);
        });

        // Enable the Organization Data Ownership policy to trigger the DefaultUserCollection creation
        if (enableOrganizationDataOwnershipPolicy)
        {
            await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);
        }

        var acceptedUsers = new List<(string email, OrganizationUser orgUser)>();

        for (int i = 0; i < 3; i++)
        {
            var acceptedUserEmail = $"{Guid.NewGuid()}@bitwarden.com";
            await _factory.LoginWithNewAccount(acceptedUserEmail);

            var acceptedOrgUser = await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, acceptedUserEmail,
                OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Accepted);

            acceptedUsers.Add((acceptedUserEmail, acceptedOrgUser));
        }

        // Login as owner for confirmation
        await _loginHelper.LoginAsync(_ownerEmail);

        var bulkConfirmModel = new OrganizationUserBulkConfirmRequestModel
        {
            Keys = acceptedUsers.Select((user, index) => new OrganizationUserBulkConfirmRequestModelEntry
            {
                Id = user.orgUser.Id,
                Key = $"test-key-{index}"
            }),
            DefaultUserCollectionName = _mockEncryptedString
        };

        var bulkConfirmResponse = await _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/confirm", bulkConfirmModel);

        Assert.Equal(HttpStatusCode.OK, bulkConfirmResponse.StatusCode);

        // Verify all users are confirmed with correct keys
        var orgUserRepository = _factory.GetService<IOrganizationUserRepository>();
        for (int i = 0; i < acceptedUsers.Count; i++)
        {
            var confirmedUser = await orgUserRepository.GetByIdAsync(acceptedUsers[i].orgUser.Id);
            Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedUser.Status);
            Assert.Equal($"test-key-{i}", confirmedUser.Key);
        }

        if (enableOrganizationDataOwnershipPolicy)
        {
            // Verify all users have a DefaultUserCollection
            var collectionRepository = _factory.GetService<ICollectionRepository>();
            foreach (var acceptedUser in acceptedUsers)
            {
                var collections = await collectionRepository.GetManyByUserIdAsync(acceptedUser.orgUser.UserId!.Value);
                Assert.Single(collections);
                Assert.Equal(_mockEncryptedString, collections.First().Name);
            }
        }
        else
        {
            // Verify all users do not have a DefaultUserCollection
            var collectionRepository = _factory.GetService<ICollectionRepository>();
            var organizationCollectionCount = await collectionRepository.GetCountByOrganizationIdAsync(_organization.Id);
            Assert.Equal(0, organizationCollectionCount);
        }
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }
}
