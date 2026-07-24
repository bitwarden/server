using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerPutTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IFeatureService _featureService;

    private Organization _organization = null!;
    private OrganizationUser _owner = null!;
    private string _ownerEmail = null!;

    public OrganizationUserControllerPutTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _featureService = _factory.GetService<IFeatureService>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-user-put-integration-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
        (_organization, _owner) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_UpdatesUser_PersistsChangesAndPreservesDefaultCollection(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        var (group, sharedCollection, defaultCollection) = await CreateTestDataAsync();
        await AssignCollectionsAsync(member, new CollectionAccessSelection { Id = defaultCollection.Id, Manage = true });

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}",
            CreateUpdateRequest(sharedCollection, group));

        Assert.Equal(ExpectedSuccess(flagOn), response.StatusCode);
        await VerifyUserWasUpdatedCorrectlyAsync(member, OrganizationUserType.Custom, expectedManageGroups: true);
        await VerifyGroupAccessWasAddedAsync(member, [group]);
        await VerifyCollectionAccessWasUpdatedCorrectlyAsync(member, sharedCollection.Id, defaultCollection.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_SelfEditWithoutAllCollectionAccess_CannotAddSelfToCollection(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);

        var (adminEmail, admin) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);

        // Another user manages the collection, so the self-editing admin genuinely cannot manage it.
        var collection = await CreateCollectionAsync();
        await AssignCollectionsAsync(_owner, new CollectionAccessSelection { Id = collection.Id, Manage = true });

        await _loginHelper.LoginAsync(adminEmail);

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Admin,
            Permissions = new Permissions(),
            Collections = [new SelectionReadOnlyRequestModel { Id = collection.Id, Manage = true }],
            Groups = []
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{admin.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertDoesNotHaveCollectionAsync(admin, collection.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_SelfEditWithoutAllCollectionAccess_DoesNotUpdateGroups(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);

        var (adminEmail, admin) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);
        var group = await CreateGroupAsync();
        await _loginHelper.LoginAsync(adminEmail);

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Admin,
            Permissions = new Permissions(),
            Collections = [],
            Groups = [group.Id]
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{admin.Id}", request);

        Assert.Equal(ExpectedSuccess(flagOn), response.StatusCode);
        var userGroups = await _factory.GetService<IGroupRepository>().GetManyIdsByUserIdAsync(admin.Id);
        Assert.DoesNotContain(group.Id, userGroups);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_SelfEditWithAllCollectionAccess_UpdatesGroups(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);

        var (adminEmail, admin) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);
        var group = await CreateGroupAsync();
        await _loginHelper.LoginAsync(adminEmail);

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Admin,
            Permissions = new Permissions(),
            Collections = [],
            Groups = [group.Id]
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{admin.Id}", request);

        Assert.Equal(ExpectedSuccess(flagOn), response.StatusCode);
        await VerifyGroupAccessWasAddedAsync(admin, [group]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_PreservesCollectionsTheSavingUserCannotManage(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);

        var editable = await CreateCollectionAsync();
        var readonly1 = await CreateCollectionAsync();
        var readonly2 = await CreateCollectionAsync();

        var (adminEmail, admin) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);
        await AssignCollectionsAsync(admin, new CollectionAccessSelection { Id = editable.Id, Manage = true });

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        await AssignCollectionsAsync(member,
            new CollectionAccessSelection { Id = editable.Id, ReadOnly = true },
            new CollectionAccessSelection { Id = readonly1.Id, Manage = true },
            new CollectionAccessSelection { Id = readonly2.Id, Manage = true });

        await _loginHelper.LoginAsync(adminEmail);

        // The admin only posts the collection it can manage; the member's other collections must be preserved.
        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions(),
            Collections = [new SelectionReadOnlyRequestModel { Id = editable.Id, Manage = true }],
            Groups = []
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}", request);

        Assert.Equal(ExpectedSuccess(flagOn), response.StatusCode);
        var access = await GetCollectionAccessAsync(member);
        Assert.Contains(access, c => c.Id == editable.Id && c.Manage);
        Assert.Contains(readonly1.Id, access.Select(c => c.Id));
        Assert.Contains(readonly2.Id, access.Select(c => c.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_SavingUserCannotManagePostedCollections_ReturnsNotFound(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);

        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Admin);
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        // Assigning the collection to the member gives it a manager, so the admin genuinely cannot manage it.
        var collection = await CreateCollectionAsync();
        await AssignCollectionsAsync(member, new CollectionAccessSelection { Id = collection.Id, Manage = true });

        await _loginHelper.LoginAsync(adminEmail);

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions(),
            Collections = [new SelectionReadOnlyRequestModel { Id = collection.Id, Manage = true }],
            Groups = []
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_AsAdminWithoutAllCollectionItemAccess_PreservesMembersDefaultCollection(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        // The admin has no manage access to the member's "My Items" collection - the condition where the update
        // flow would otherwise drop it from the collections to save.
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);

        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Admin);
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        var defaultCollection = await CreateCollectionAsync(CollectionType.DefaultUserCollection);
        await AssignCollectionsAsync(member, new CollectionAccessSelection { Id = defaultCollection.Id, Manage = true });

        await _loginHelper.LoginAsync(adminEmail);

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions(),
            Collections = [],
            Groups = []
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}", request);

        Assert.Equal(ExpectedSuccess(flagOn), response.StatusCode);
        await AssertHasCollectionAsync(member, defaultCollection.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_RemovingLastConfirmedOwner_ReturnsBadRequest(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        await _loginHelper.LoginAsync(_ownerEmail);

        // Demoting the organization's only confirmed owner is rejected in both paths.
        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Admin,
            Permissions = new Permissions(),
            Collections = [],
            Groups = []
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{_owner.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_PostingDefaultCollection_IsIgnored(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp).Returns(flagOn);
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        var defaultCollection = await CreateCollectionAsync(CollectionType.DefaultUserCollection);

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions(),
            Collections = [new SelectionReadOnlyRequestModel { Id = defaultCollection.Id, Manage = true }],
            Groups = []
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}", request);

        Assert.Equal(ExpectedSuccess(flagOn), response.StatusCode);
        await AssertDoesNotHaveCollectionAsync(member, defaultCollection.Id);
    }

    private static HttpStatusCode ExpectedSuccess(bool flagOn) =>
        flagOn ? HttpStatusCode.NoContent : HttpStatusCode.OK;

    private async Task SetAllowAdminAccessToAllCollectionItemsAsync(bool value)
    {
        _organization.AllowAdminAccessToAllCollectionItems = value;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);
    }

    private async Task<Group> CreateGroupAsync() =>
        await _factory.GetService<IGroupRepository>().CreateAsync(new Group
        {
            OrganizationId = _organization.Id,
            Name = $"Test Group {Guid.NewGuid()}"
        });

    private async Task<Collection> CreateCollectionAsync(CollectionType type = CollectionType.SharedCollection) =>
        await _factory.GetService<ICollectionRepository>().CreateAsync(new Collection
        {
            OrganizationId = _organization.Id,
            Name = $"Test Collection {Guid.NewGuid()}",
            Type = type
        });

    private async Task<(Group group, Collection sharedCollection, Collection defaultCollection)> CreateTestDataAsync() =>
        (await CreateGroupAsync(),
            await CreateCollectionAsync(CollectionType.SharedCollection),
            await CreateCollectionAsync(CollectionType.DefaultUserCollection));

    private async Task AssignCollectionsAsync(OrganizationUser organizationUser, params CollectionAccessSelection[] access) =>
        await _factory.GetService<IOrganizationUserRepository>().ReplaceAsync(organizationUser, access.ToList());

    private static OrganizationUserUpdateRequestModel CreateUpdateRequest(Collection sharedCollection, Group group) =>
        new()
        {
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { ManageGroups = true },
            Collections = [new SelectionReadOnlyRequestModel { Id = sharedCollection.Id, ReadOnly = true }],
            Groups = [group.Id]
        };

    private async Task<ICollection<CollectionAccessSelection>> GetCollectionAccessAsync(OrganizationUser organizationUser)
    {
        var (_, access) = await _factory.GetService<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUser.Id);
        return access;
    }

    private async Task AssertHasCollectionAsync(OrganizationUser organizationUser, Guid collectionId)
    {
        var access = await GetCollectionAccessAsync(organizationUser);
        Assert.Contains(collectionId, access.Select(c => c.Id));
    }

    private async Task AssertDoesNotHaveCollectionAsync(OrganizationUser organizationUser, Guid collectionId)
    {
        var access = await GetCollectionAccessAsync(organizationUser);
        Assert.DoesNotContain(collectionId, access.Select(c => c.Id));
    }

    private async Task VerifyUserWasUpdatedCorrectlyAsync(OrganizationUser organizationUser,
        OrganizationUserType expectedType, bool expectedManageGroups)
    {
        var updatedOrgUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(expectedType, updatedOrgUser.Type);
        Assert.Equal(expectedManageGroups, updatedOrgUser.GetPermissions().ManageGroups);
    }

    private async Task VerifyGroupAccessWasAddedAsync(OrganizationUser organizationUser, IEnumerable<Group> groups)
    {
        var userGroups = await _factory.GetService<IGroupRepository>().GetManyIdsByUserIdAsync(organizationUser.Id);
        Assert.All(groups, group => Assert.Contains(group.Id, userGroups));
    }

    private async Task VerifyCollectionAccessWasUpdatedCorrectlyAsync(OrganizationUser organizationUser,
        Guid sharedCollectionId, Guid defaultCollectionId)
    {
        var access = await GetCollectionAccessAsync(organizationUser);
        Assert.Contains(defaultCollectionId, access.Select(c => c.Id));

        var sharedAccess = access.First(c => c.Id == sharedCollectionId);
        Assert.True(sharedAccess.ReadOnly);
        Assert.False(sharedAccess.HidePasswords);
        Assert.False(sharedAccess.Manage);
    }
}
