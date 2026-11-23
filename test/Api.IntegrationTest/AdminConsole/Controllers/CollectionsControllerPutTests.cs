using System.Net;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Test.Common.Constants;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

/// <summary>
/// Authorization tests for <c>PUT organizations/{orgId}/collections/{id}</c>. Every case runs with
/// <see cref="FeatureFlagKeys.AuthorizationServices"/> on and off. Where the two paths differ, a comment says why.
/// </summary>
public class CollectionsControllerPutTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _originalExternalId = "original-external-id";
    private const string _updatedExternalId = "updated-external-id";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IFeatureService _featureService;

    private Organization _organization = null!;
    private OrganizationUser _owner = null!;

    public CollectionsControllerPutTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _featureService = _factory.GetService<IFeatureService>();
    }

    public async Task InitializeAsync()
    {
        (_organization, _owner) = await SignUpOrganizationAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner, true)]
    [InlineData(OrganizationUserType.Owner, false)]
    [InlineData(OrganizationUserType.Admin, true)]
    [InlineData(OrganizationUserType.Admin, false)]
    public async Task Put_AsOwnerOrAdmin_WithAdminAccessToAllCollections_UpdatesCollection(
        OrganizationUserType type, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var (email, _) = await CreateMemberAsync(type);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertUpdatedAsync(response, collection.Id);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner, true)]
    [InlineData(OrganizationUserType.Owner, false)]
    [InlineData(OrganizationUserType.Admin, true)]
    [InlineData(OrganizationUserType.Admin, false)]
    public async Task Put_AsOwnerOrAdmin_WithoutAdminAccessToAllCollections_ReturnsNotFound(
        OrganizationUserType type, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var (email, _) = await CreateMemberAsync(type);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner, true)]
    [InlineData(OrganizationUserType.Owner, false)]
    [InlineData(OrganizationUserType.Admin, true)]
    [InlineData(OrganizationUserType.Admin, false)]
    public async Task Put_AsOwnerOrAdmin_WhenCollectionIsOrphaned_UpdatesCollection(
        OrganizationUserType type, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionAsync();
        var (email, organizationUser) = await CreateMemberAsync(type);

        // The update must leave someone with Manage, or the command rejects it.
        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled,
            users: [new SelectionReadOnlyRequestModel { Id = organizationUser.Id, Manage = true }]);

        await AssertUpdatedAsync(response, collection.Id);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner, true)]
    [InlineData(OrganizationUserType.Owner, false)]
    [InlineData(OrganizationUserType.Admin, true)]
    [InlineData(OrganizationUserType.Admin, false)]
    public async Task Put_AsOwnerOrAdmin_WhenOnlyManagerIsRevoked_ReturnsNotFound(
        OrganizationUserType type, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var (_, revokedMember) = await CreateMemberAsync(OrganizationUserType.User,
            status: OrganizationUserStatusType.Revoked);
        // A revoked member's Manage still counts, so the collection is not orphaned.
        var collection = await CreateCollectionAsync(users:
            [new CollectionAccessSelection { Id = revokedMember.Id, Manage = true }]);
        var (email, _) = await CreateMemberAsync(type);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(OrganizationUserType.User, true)]
    [InlineData(OrganizationUserType.User, false)]
    [InlineData(OrganizationUserType.Custom, true)]
    [InlineData(OrganizationUserType.Custom, false)]
    public async Task Put_AsUserOrCustom_WhenCollectionIsOrphaned_ReturnsNotFound(
        OrganizationUserType type, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionAsync();
        var (email, organizationUser) = await CreateMemberAsync(type);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled,
            users: [new SelectionReadOnlyRequestModel { Id = organizationUser.Id, Manage = true }]);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Put_WithEditAnyCollectionPermission_UpdatesCollection(
        bool allowAdminAccessToAllCollectionItems, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(allowAdminAccessToAllCollectionItems);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var (email, _) = await CreateMemberAsync(OrganizationUserType.Custom,
            new Permissions { EditAnyCollection = true });

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertUpdatedAsync(response, collection.Id);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Put_WithOnlyManageUsersOrManageGroupsPermission_ReturnsNotFound(
        bool manageUsers, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var (email, _) = await CreateMemberAsync(OrganizationUserType.Custom,
            new Permissions { ManageUsers = manageUsers, ManageGroups = !manageUsers });

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Put_WhenMemberCanManageCollection_UpdatesCollection(
        bool manageThroughGroup, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var (email, organizationUser) = await CreateMemberAsync(OrganizationUserType.User);
        var collection = manageThroughGroup
            ? await CreateCollectionAsync(groups:
                [new CollectionAccessSelection { Id = (await CreateGroupWithMemberAsync(organizationUser.Id)).Id, Manage = true }])
            : await CreateCollectionAsync(users:
                [new CollectionAccessSelection { Id = organizationUser.Id, Manage = true }]);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertUpdatedAsync(response, collection.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_WhenMemberCanEditWithoutManage_ReturnsNotFound(bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var (email, organizationUser) = await CreateMemberAsync(OrganizationUserType.User);
        var collection = await CreateCollectionAsync(users:
        [
            new CollectionAccessSelection { Id = _owner.Id, Manage = true },
            new CollectionAccessSelection { Id = organizationUser.Id, ReadOnly = false, Manage = false }
        ]);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Accepted, true)]
    [InlineData(OrganizationUserStatusType.Accepted, false)]
    [InlineData(OrganizationUserStatusType.Revoked, true)]
    [InlineData(OrganizationUserStatusType.Revoked, false)]
    public async Task Put_WhenUnconfirmedMemberCanManageCollection_ReturnsNotFound(
        OrganizationUserStatusType status, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var (email, organizationUser) = await CreateMemberAsync(OrganizationUserType.User, status: status);
        var collection = await CreateCollectionAsync(users:
        [
            new CollectionAccessSelection { Id = _owner.Id, Manage = true },
            new CollectionAccessSelection { Id = organizationUser.Id, Manage = true }
        ]);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_WhenOrganizationIsDisabled_ReturnsNotFound(bool authorizationServicesEnabled)
    {
        var (email, organizationUser) = await CreateMemberAsync(OrganizationUserType.User);
        var collection = await CreateCollectionAsync(users:
            [new CollectionAccessSelection { Id = organizationUser.Id, Manage = true }]);
        _organization.Enabled = false;
        await SaveOrganizationAsync();

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_WhenCollectionBelongsToAnotherOrganization_ReturnsNotFound(bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);
        var (email, _) = await CreateMemberAsync(OrganizationUserType.Owner);
        var (otherOrganization, otherOwner) = await SignUpOrganizationAsync();
        var collection = await OrganizationTestHelpers.CreateCollectionAsync(_factory, otherOrganization.Id,
            $"collection-{Guid.NewGuid()}", externalId: _originalExternalId,
            users: [new CollectionAccessSelection { Id = otherOwner.Id, Manage = true }]);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_WhenCollectionDoesNotExist_ReturnsNotFound(bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);
        var (email, _) = await CreateMemberAsync(OrganizationUserType.Owner);

        var response = await PutAsync(email, Guid.NewGuid(), authorizationServicesEnabled);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(ProviderType.Msp, ProviderUserType.ProviderAdmin, true)]
    [InlineData(ProviderType.Msp, ProviderUserType.ProviderAdmin, false)]
    [InlineData(ProviderType.Msp, ProviderUserType.ServiceUser, true)]
    [InlineData(ProviderType.Msp, ProviderUserType.ServiceUser, false)]
    [InlineData(ProviderType.BusinessUnit, ProviderUserType.ProviderAdmin, true)]
    [InlineData(ProviderType.BusinessUnit, ProviderUserType.ProviderAdmin, false)]
    [InlineData(ProviderType.BusinessUnit, ProviderUserType.ServiceUser, true)]
    [InlineData(ProviderType.BusinessUnit, ProviderUserType.ServiceUser, false)]
    public async Task Put_AsProviderUser_UpdatesCollection(
        ProviderType providerType, ProviderUserType providerUserType, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var email = await CreateProviderUserAsync(providerType, providerUserType);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertUpdatedAsync(response, collection.Id);
    }

    [Theory]
    [InlineData(ProviderType.Msp, true)]
    [InlineData(ProviderType.Msp, false)]
    [InlineData(ProviderType.BusinessUnit, true)]
    [InlineData(ProviderType.BusinessUnit, false)]
    public async Task Put_AsProviderUserWhoIsAlsoAMember_UpdatesCollection(
        ProviderType providerType, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var email = await CreateProviderUserAsync(providerType, ProviderUserType.ServiceUser);
        await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email, OrganizationUserType.User);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertUpdatedAsync(response, collection.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_WhenProviderUserIsNotConfirmed_ReturnsNotFound(bool authorizationServicesEnabled)
    {
        var collection = await CreateCollectionManagedByOwnerAsync();
        var email = await CreateAccountAsync();
        var provider = await ProviderTestHelpers.CreateProviderAndLinkToOrganizationAsync(
            _factory, _organization.Id, ProviderType.Msp);
        var user = await _factory.GetService<IUserRepository>().GetByEmailAsync(email);
        await _factory.GetService<IProviderUserRepository>().CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user!.Id,
            Status = ProviderUserStatusType.Accepted,
            Type = ProviderUserType.ServiceUser,
        });

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_AsProviderUserForAnotherOrganization_ReturnsNotFound(bool authorizationServicesEnabled)
    {
        var collection = await CreateCollectionManagedByOwnerAsync();
        var (otherOrganization, _) = await SignUpOrganizationAsync();
        var email = await CreateProviderUserAsync(ProviderType.Msp, ProviderUserType.ServiceUser, otherOrganization.Id);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Fact]
    public async Task Put_AsResellerProviderUser_ReturnsNotFound()
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var email = await CreateProviderUserAsync(ProviderType.Reseller, ProviderUserType.ServiceUser);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled: true);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    // TODO: Remove with the AuthorizationServices flag. The legacy handler allows reseller providers.
    [Fact]
    public async Task Put_AsResellerProviderUser_WithLegacyAuthorization_UpdatesCollection()
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var email = await CreateProviderUserAsync(ProviderType.Reseller, ProviderUserType.ServiceUser);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled: false);

        await AssertUpdatedAsync(response, collection.Id);
    }

    [Theory]
    [InlineData(ProviderType.Msp)]
    [InlineData(ProviderType.BusinessUnit)]
    public async Task Put_AsUserOfDisabledProvider_ReturnsNotFound(ProviderType providerType)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var email = await CreateUserOfDisabledProviderAsync(providerType);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled: true);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    // TODO: Remove with the AuthorizationServices flag. The legacy handler allows disabled providers.
    [Theory]
    [InlineData(ProviderType.Msp)]
    [InlineData(ProviderType.BusinessUnit)]
    public async Task Put_AsUserOfDisabledProvider_WithLegacyAuthorization_UpdatesCollection(ProviderType providerType)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var collection = await CreateCollectionManagedByOwnerAsync();
        var email = await CreateUserOfDisabledProviderAsync(providerType);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled: false);

        await AssertUpdatedAsync(response, collection.Id);
    }

    // The default user collection tests below expect a different status for each flag state. The authorization service
    // rejects a default user collection (404). The legacy handler authorizes the request and the command rejects it (400).
    // TODO: Remove the BadRequest cases with the AuthorizationServices flag.
    [Theory]
    [InlineData(true, HttpStatusCode.NotFound)]
    [InlineData(false, HttpStatusCode.BadRequest)]
    public async Task Put_DefaultCollection_AsAssignedMember_IsRejected(
        bool authorizationServicesEnabled, HttpStatusCode expectedStatusCode)
    {
        var (email, organizationUser) = await CreateMemberAsync(OrganizationUserType.User);
        var collection = await CreateDefaultCollectionAsync(organizationUser.Id);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, expectedStatusCode, collection);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_DefaultCollection_AsAnotherMember_ReturnsNotFound(bool authorizationServicesEnabled)
    {
        var (_, assignedMember) = await CreateMemberAsync(OrganizationUserType.User);
        var collection = await CreateDefaultCollectionAsync(assignedMember.Id);
        var (email, _) = await CreateMemberAsync(OrganizationUserType.User);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner, true, HttpStatusCode.NotFound)]
    [InlineData(OrganizationUserType.Owner, false, HttpStatusCode.BadRequest)]
    [InlineData(OrganizationUserType.Admin, true, HttpStatusCode.NotFound)]
    [InlineData(OrganizationUserType.Admin, false, HttpStatusCode.BadRequest)]
    public async Task Put_DefaultCollection_AsOwnerOrAdminWithAdminAccessToAllCollections_IsRejected(
        OrganizationUserType type, bool authorizationServicesEnabled, HttpStatusCode expectedStatusCode)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);
        var (_, assignedMember) = await CreateMemberAsync(OrganizationUserType.User);
        var collection = await CreateDefaultCollectionAsync(assignedMember.Id);
        var (email, _) = await CreateMemberAsync(type);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, expectedStatusCode, collection);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.NotFound)]
    [InlineData(false, HttpStatusCode.BadRequest)]
    public async Task Put_DefaultCollection_WithEditAnyCollectionPermission_IsRejected(
        bool authorizationServicesEnabled, HttpStatusCode expectedStatusCode)
    {
        var (_, assignedMember) = await CreateMemberAsync(OrganizationUserType.User);
        var collection = await CreateDefaultCollectionAsync(assignedMember.Id);
        var (email, _) = await CreateMemberAsync(OrganizationUserType.Custom,
            new Permissions { EditAnyCollection = true });

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, expectedStatusCode, collection);
    }

    [Theory]
    [InlineData(ProviderType.Msp, true, HttpStatusCode.NotFound)]
    [InlineData(ProviderType.Msp, false, HttpStatusCode.BadRequest)]
    [InlineData(ProviderType.BusinessUnit, true, HttpStatusCode.NotFound)]
    [InlineData(ProviderType.BusinessUnit, false, HttpStatusCode.BadRequest)]
    public async Task Put_DefaultCollection_AsProviderUser_IsRejected(
        ProviderType providerType, bool authorizationServicesEnabled, HttpStatusCode expectedStatusCode)
    {
        var (_, assignedMember) = await CreateMemberAsync(OrganizationUserType.User);
        var collection = await CreateDefaultCollectionAsync(assignedMember.Id);
        var email = await CreateProviderUserAsync(providerType, ProviderUserType.ServiceUser);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, expectedStatusCode, collection);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner, true)]
    [InlineData(OrganizationUserType.Owner, false)]
    [InlineData(OrganizationUserType.Admin, true)]
    [InlineData(OrganizationUserType.Admin, false)]
    public async Task Put_DefaultCollectionOfRevokedMember_AsOwnerOrAdminWithoutAdminAccess_ReturnsNotFound(
        OrganizationUserType type, bool authorizationServicesEnabled)
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(false);
        var (_, revokedMember) = await CreateMemberAsync(OrganizationUserType.User,
            status: OrganizationUserStatusType.Revoked);
        var collection = await CreateDefaultCollectionAsync(revokedMember.Id);
        var (email, _) = await CreateMemberAsync(type);

        var response = await PutAsync(email, collection.Id, authorizationServicesEnabled);

        await AssertNotUpdatedAsync(response, HttpStatusCode.NotFound, collection);
    }

    private async Task<(Organization Organization, OrganizationUser Owner)> SignUpOrganizationAsync()
    {
        var ownerEmail = await CreateAccountAsync();
        var (organization, owner) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        return (organization, owner);
    }

    private async Task<string> CreateAccountAsync()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        return email;
    }

    private async Task<(string Email, OrganizationUser OrganizationUser)> CreateMemberAsync(
        OrganizationUserType type,
        Permissions? permissions = null,
        OrganizationUserStatusType status = OrganizationUserStatusType.Confirmed)
    {
        var email = await CreateAccountAsync();
        var organizationUser = await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email, type,
            permissions: permissions, userStatusType: status);
        return (email, organizationUser);
    }

    private async Task<string> CreateProviderUserAsync(
        ProviderType providerType, ProviderUserType providerUserType, Guid? organizationId = null)
    {
        var email = await CreateAccountAsync();
        var provider = await ProviderTestHelpers.CreateProviderAndLinkToOrganizationAsync(
            _factory, organizationId ?? _organization.Id, providerType);
        await ProviderTestHelpers.CreateProviderUserAsync(_factory, provider.Id, email, providerUserType);
        return email;
    }

    private async Task<string> CreateUserOfDisabledProviderAsync(ProviderType providerType)
    {
        var email = await CreateProviderUserAsync(providerType, ProviderUserType.ServiceUser);
        var providerRepository = _factory.GetService<IProviderRepository>();
        var provider = await providerRepository.GetByOrganizationIdAsync(_organization.Id);
        provider!.Enabled = false;
        await providerRepository.ReplaceAsync(provider);
        return email;
    }

    private async Task<Group> CreateGroupWithMemberAsync(Guid organizationUserId)
    {
        var group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);
        await _factory.GetService<IGroupRepository>()
            .AddGroupUsersByIdAsync(group.Id, [organizationUserId], DateTime.UtcNow);
        return group;
    }

    private Task<Collection> CreateCollectionAsync(
        IEnumerable<CollectionAccessSelection>? users = null,
        IEnumerable<CollectionAccessSelection>? groups = null) =>
        OrganizationTestHelpers.CreateCollectionAsync(_factory, _organization.Id, $"collection-{Guid.NewGuid()}",
            users: users, groups: groups, externalId: _originalExternalId);

    private Task<Collection> CreateCollectionManagedByOwnerAsync() =>
        CreateCollectionAsync(users: [new CollectionAccessSelection { Id = _owner.Id, Manage = true }]);

    private async Task<Collection> CreateDefaultCollectionAsync(Guid organizationUserId)
    {
        await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        await collectionRepository.CreateDefaultCollectionsAsync(_organization.Id, [organizationUserId], "My Items");
        return (await collectionRepository.GetManyByOrganizationIdAsync(_organization.Id))
            .Single(c => c.Type == CollectionType.DefaultUserCollection);
    }

    private async Task SetAllowAdminAccessToAllCollectionItemsAsync(bool value)
    {
        _organization.AllowAdminAccessToAllCollectionItems = value;
        await SaveOrganizationAsync();
    }

    private async Task SaveOrganizationAsync()
    {
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);
        // ReplaceAsync does not refresh the ability cache.
        await _factory.GetService<IOrganizationAbilityCacheService>().UpsertOrganizationAbilityAsync(_organization);
    }

    private async Task<HttpResponseMessage> PutAsync(
        string email,
        Guid collectionId,
        bool authorizationServicesEnabled,
        IEnumerable<SelectionReadOnlyRequestModel>? users = null)
    {
        _featureService.IsEnabled(FeatureFlagKeys.AuthorizationServices).Returns(authorizationServicesEnabled);
        await _loginHelper.LoginAsync(email);

        return await _client.PutAsJsonAsync($"organizations/{_organization.Id}/collections/{collectionId}",
            new UpdateCollectionRequestModel
            {
                Name = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
                ExternalId = _updatedExternalId,
                Users = users,
            });
    }

    private async Task AssertUpdatedAsync(HttpResponseMessage response, Guid collectionId)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_updatedExternalId, await GetExternalIdAsync(collectionId));
    }

    private async Task AssertNotUpdatedAsync(
        HttpResponseMessage response, HttpStatusCode expectedStatusCode, Collection collection)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(collection.ExternalId, await GetExternalIdAsync(collection.Id));
    }

    private async Task<string?> GetExternalIdAsync(Guid collectionId) =>
        (await _factory.GetService<ICollectionRepository>().GetByIdAsync(collectionId))!.ExternalId;
}
