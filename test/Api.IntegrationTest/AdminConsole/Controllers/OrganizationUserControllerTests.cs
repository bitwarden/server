using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.Repositories;
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

    [Fact]
    public async Task Put_WithExistingDefaultCollection_Success()
    {
        // Arrange
        await _loginHelper.LoginAsync(_ownerEmail);

        var (userEmail, organizationUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.User);

        var (group, sharedCollection, defaultCollection) = await CreateTestDataAsync();
        await AssignDefaultCollectionToUserAsync(organizationUser, defaultCollection);

        // Act
        var updateRequest = CreateUpdateRequest(sharedCollection, group);
        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{organizationUser.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);

        // Assert
        await VerifyUserWasUpdatedCorrectlyAsync(organizationUser, expectedType: OrganizationUserType.Custom, expectedManageGroups: true);
        await VerifyGroupAccessWasAddedAsync(organizationUser, [group]);
        await VerifyCollectionAccessWasUpdatedCorrectlyAsync(organizationUser, sharedCollection.Id, defaultCollection.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(Group group, Collection sharedCollection, Collection defaultCollection)> CreateTestDataAsync()
    {
        var groupRepository = _factory.GetService<IGroupRepository>();
        var group = await groupRepository.CreateAsync(new Group
        {
            OrganizationId = _organization.Id,
            Name = $"Test Group {Guid.NewGuid()}"
        });

        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var sharedCollection = await collectionRepository.CreateAsync(new Collection
        {
            OrganizationId = _organization.Id,
            Name = $"Test Collection {Guid.NewGuid()}",
            Type = CollectionType.SharedCollection
        });

        var defaultCollection = await collectionRepository.CreateAsync(new Collection
        {
            OrganizationId = _organization.Id,
            Name = $"My Items {Guid.NewGuid()}",
            Type = CollectionType.DefaultUserCollection
        });

        return (group, sharedCollection, defaultCollection);
    }

    private async Task AssignDefaultCollectionToUserAsync(OrganizationUser organizationUser, Collection defaultCollection)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        await organizationUserRepository.ReplaceAsync(organizationUser,
            new List<CollectionAccessSelection>
            {
                new CollectionAccessSelection
                {
                    Id = defaultCollection.Id,
                    ReadOnly = false,
                    HidePasswords = false,
                    Manage = true
                }
            });
    }

    private static OrganizationUserUpdateRequestModel CreateUpdateRequest(Collection sharedCollection, Group group)
    {
        return new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions
            {
                ManageGroups = true
            },
            Collections = new List<SelectionReadOnlyRequestModel>
            {
                new SelectionReadOnlyRequestModel
                {
                    Id = sharedCollection.Id,
                    ReadOnly = true,
                    HidePasswords = false,
                    Manage = false
                }
            },
            Groups = new List<Guid> { group.Id }
        };
    }

    private async Task VerifyUserWasUpdatedCorrectlyAsync(
        OrganizationUser organizationUser,
        OrganizationUserType expectedType,
        bool expectedManageGroups)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var updatedOrgUser = await organizationUserRepository.GetByIdAsync(organizationUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(expectedType, updatedOrgUser.Type);
        Assert.Equal(expectedManageGroups, updatedOrgUser.GetPermissions().ManageGroups);
    }

    private async Task VerifyGroupAccessWasAddedAsync(
        OrganizationUser organizationUser, IEnumerable<Group> groups)
    {
        var groupRepository = _factory.GetService<IGroupRepository>();
        var userGroups = await groupRepository.GetManyIdsByUserIdAsync(organizationUser.Id);
        Assert.All(groups, group => Assert.Contains(group.Id, userGroups));
    }

    private async Task VerifyCollectionAccessWasUpdatedCorrectlyAsync(
        OrganizationUser organizationUser, Guid sharedCollectionId, Guid defaultCollectionId)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var (_, collectionAccess) = await organizationUserRepository.GetByIdWithCollectionsAsync(organizationUser.Id);
        var collectionIds = collectionAccess.Select(c => c.Id).ToHashSet();

        Assert.Contains(defaultCollectionId, collectionIds);
        Assert.Contains(sharedCollectionId, collectionIds);

        var newCollectionAccess = collectionAccess.First(c => c.Id == sharedCollectionId);
        Assert.True(newCollectionAccess.ReadOnly);
        Assert.False(newCollectionAccess.HidePasswords);
        Assert.False(newCollectionAccess.Manage);
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
