using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerPutTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private OrganizationUser _owner = null!;
    private string _ownerEmail = null!;

    public OrganizationUserControllerPutTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
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

    [Fact]
    public async Task Put_UpdatesUser_PersistsChangesAndPreservesDefaultCollection()
    {
        await SetAllowAdminAccessToAllCollectionItemsAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        var (group, sharedCollection, defaultCollection) = await CreateTestDataAsync();
        await AssignCollectionsAsync(member, new CollectionAccessSelection { Id = defaultCollection.Id, Manage = true });

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}",
            CreateUpdateRequest(sharedCollection, group));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await VerifyUserWasUpdatedCorrectlyAsync(member, OrganizationUserType.Custom, expectedManageGroups: true);
        await VerifyGroupAccessWasAddedAsync(member, [group]);
        await VerifyCollectionAccessWasUpdatedCorrectlyAsync(member, sharedCollection.Id, defaultCollection.Id);
    }

    [Fact]
    public async Task Put_ConvertingMemberFromCustom_ClearsPermissions()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        // Give the member the Custom role with a persisted permission set.
        member.Type = OrganizationUserType.Custom;
        member.SetPermissions(new Permissions { ManageGroups = true });
        await _factory.GetService<IOrganizationUserRepository>().ReplaceAsync(member);

        // Converting to a non-Custom role must drop the stored permissions.
        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions { ManageGroups = true },
            Collections = [],
            Groups = []
        };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var updatedOrgUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(member.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserType.User, updatedOrgUser.Type);
        Assert.Null(updatedOrgUser.Permissions);
    }

    [Fact]
    public async Task Put_SelfEditWithoutAllCollectionAccess_CannotAddSelfToCollection()
    {
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

    [Fact]
    public async Task Put_SelfEditWithoutAllCollectionAccess_DoesNotUpdateGroups()
    {
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

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var userGroups = await _factory.GetService<IGroupRepository>().GetManyIdsByUserIdAsync(admin.Id);
        Assert.DoesNotContain(group.Id, userGroups);
    }

    [Fact]
    public async Task Put_SelfEditWithAllCollectionAccess_UpdatesGroups()
    {
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

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await VerifyGroupAccessWasAddedAsync(admin, [group]);
    }

    [Fact]
    public async Task Put_PreservesCollectionsTheSavingUserCannotManage()
    {
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

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var access = await GetCollectionAccessAsync(member);
        Assert.Contains(access, c => c.Id == editable.Id && c.Manage);
        Assert.Contains(readonly1.Id, access.Select(c => c.Id));
        Assert.Contains(readonly2.Id, access.Select(c => c.Id));
    }

    [Fact]
    public async Task Put_SavingUserCannotManagePostedCollections_ReturnsNotFound()
    {
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

    [Fact]
    public async Task Put_AsAdminWithoutAllCollectionItemAccess_PreservesMembersDefaultCollection()
    {
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

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertHasCollectionAsync(member, defaultCollection.Id);
    }

    [Fact]
    public async Task Put_RemovingLastConfirmedOwner_ReturnsBadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        // Demoting the organization's only confirmed owner is rejected.
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

    [Fact]
    public async Task Put_PostingDefaultCollection_IsIgnored()
    {
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

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertDoesNotHaveCollectionAsync(member, defaultCollection.Id);
    }

    [Fact]
    public async Task Put_GrantingPam_PersistsAccessPam()
    {
        await SetUsePamAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(accessPam: true));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertAccessPamAsync(member, true);
    }

    [Fact]
    public async Task Put_GrantingPamWithoutOrganizationPam_ReturnsBadRequest()
    {
        await SetUsePamAsync(false);
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(accessPam: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new PamNotEnabled().Message, await response.Content.ReadAsStringAsync());
        await AssertAccessPamAsync(member, false);
    }

    [Fact]
    public async Task Put_RevokingPamWithoutOrganizationPam_PersistsRevocation()
    {
        // Revoking access must stay possible on an organization whose PAM entitlement has lapsed.
        await SetUsePamAsync(false);
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        await GrantPamAsync(member);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(accessPam: false));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertAccessPamAsync(member, false);
    }

    [Fact]
    public async Task Put_EditingMemberWhoAlreadyHasPamWithoutOrganizationPam_PreservesAccess()
    {
        // Not a grant, so an unrelated edit to a member who already has access is not blocked.
        await SetUsePamAsync(false);
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        await GrantPamAsync(member);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(accessPam: true));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertAccessPamAsync(member, true);
    }

    [Fact]
    public async Task Put_OmittingAccessPam_RevokesExistingAccess()
    {
        await SetUsePamAsync(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        await GrantPamAsync(member);

        // A client that predates the field sends no accessPam at all, which binds to the default of false.
        // Callers must send the member's current value to preserve it.
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{member.Id}",
            new { type = OrganizationUserType.User, permissions = new Permissions(), collections = Array.Empty<object>(), groups = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertAccessPamAsync(member, false);
    }

    [Fact]
    public async Task Put_AdminGrantingPamToSelf_Succeeds()
    {
        await SetUsePamAsync(true);

        // ManageUsers is the only gate on the endpoint, so an admin can grant themselves the access.
        var (adminEmail, admin) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);
        await _loginHelper.LoginAsync(adminEmail);

        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{admin.Id}",
            UpdateRequest(accessPam: true, type: OrganizationUserType.Admin));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertAccessPamAsync(admin, true);
    }

    [Fact]
    public async Task Put_WhenChangingRoleAndNameForClaimedMember_ReturnsNoContentAndPersistsBoth()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, _) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        var request = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Admin,
            Permissions = new Permissions(),
            Collections = [],
            Groups = [],
            Name = "Updated Name"
        };

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedOrgUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(member.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserType.Admin, updatedOrgUser.Type);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(member.UserId!.Value);
        Assert.NotNull(updatedUser);
        Assert.Equal("Updated Name", updatedUser.Name);
    }

    [Fact]
    public async Task Put_WhenChangingEmailForClaimedMember_ReturnsNoContentAndPersistsEmail()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, domain) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        var newEmail = $"new-{Guid.NewGuid()}@{domain}";
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", UpdateRequest(email: newEmail));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(member.UserId!.Value);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email, ignoreCase: true);
        Assert.True(updatedUser.EmailVerified);
    }

    [Fact]
    public async Task Put_WhenChangingEmailAndNameForClaimedMember_PersistsBoth()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, domain) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        var newEmail = $"new-{Guid.NewGuid()}@{domain}";
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", UpdateRequest(email: newEmail, name: "Updated Name"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(member.UserId!.Value);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email, ignoreCase: true);
        Assert.Equal("Updated Name", updatedUser.Name);
    }

    [Fact]
    public async Task Put_WhenChangingEmailForUnclaimedMember_ReturnsNotClaimedProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        // No verified domain is created, so the member is not claimed by the organization.
        var memberEmail = $"unclaimed-{Guid.NewGuid()}@bitwarden.com";
        var (_, member) = await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(
            _factory, memberEmail, _organization.Id);

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(email: $"new-{Guid.NewGuid()}@bitwarden.com"));

        await AssertValidationProblemAsync(response, new MemberNotClaimedError());
    }

    [Fact]
    public async Task Put_WhenChangingNameForUnclaimedMember_ReturnsNotClaimedProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        // No verified domain is created, so the member is not claimed by the organization.
        var memberEmail = $"unclaimed-{Guid.NewGuid()}@bitwarden.com";
        var (_, member) = await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(
            _factory, memberEmail, _organization.Id);

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", UpdateRequest(name: "Updated Name"));

        await AssertValidationProblemAsync(response, new NameChangeMemberNotClaimedError());
    }

    [Fact]
    public async Task Put_WhenChangingEmailForMemberWithMasterPassword_ReturnsHasMasterPasswordProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        // CreateNewUserWithAccountAsync registers a real account, which has a master password.
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(email: $"new-{Guid.NewGuid()}@bitwarden.com"));

        await AssertValidationProblemAsync(response, new MemberHasMasterPasswordError());
    }

    [Fact]
    public async Task Put_WhenChangingEmailToUnverifiedDomain_ReturnsDomainNotClaimedProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, _) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        var unverifiedDomain = OrganizationTestHelpers.GenerateRandomDomain();
        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}",
            UpdateRequest(email: $"new-{Guid.NewGuid()}@{unverifiedDomain}"));

        await AssertValidationProblemAsync(response, new NewEmailDomainNotClaimedError());
    }

    [Fact]
    public async Task Put_WhenChangingEmailToAddressAlreadyInUse_ReturnsAlreadyInUseProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, domain) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        // Same-domain address already taken, so domain validation passes and the uniqueness check rejects it.
        var takenEmail = $"taken-{Guid.NewGuid()}@{domain}";
        await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(_factory, takenEmail, _organization.Id);

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", UpdateRequest(email: takenEmail));

        await AssertValidationProblemAsync(response, new EmailAlreadyInUseByAnotherMemberError());
    }

    [Fact]
    public async Task Put_WhenChangingEmailToAddressTakenOutsideOrganization_ReturnsTakenOutsideOrganizationProblemDetails()
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var (member, domain) = await CreateClaimedMemberWithoutMasterPasswordAsync();

        // Same-domain address owned by an account that is not a member of the organization, so
        // domain validation passes and the uniqueness check identifies it as taken outside the org.
        var takenEmail = $"outsider-{Guid.NewGuid()}@{domain}";
        await _factory.LoginWithNewAccount(takenEmail);

        var response = await _client.PutAsJsonAsync(
            $"organizations/{_organization.Id}/users/{member.Id}", UpdateRequest(email: takenEmail));

        await AssertValidationProblemAsync(response, new EmailTakenOutsideOrganizationError());
    }

    private async Task SetAllowAdminAccessToAllCollectionItemsAsync(bool value)
    {
        _organization.AllowAdminAccessToAllCollectionItems = value;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);
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

    // A master-password-less member on a verified org domain is "claimed" and eligible for an email change.
    private async Task<(OrganizationUser Member, string Domain)> CreateClaimedMemberWithoutMasterPasswordAsync()
    {
        var domain = OrganizationTestHelpers.GenerateRandomDomain();
        _organization.UseOrganizationDomains = true;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);
        await OrganizationTestHelpers.CreateVerifiedDomainAsync(_factory, _organization.Id, domain);

        var (_, member) = await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(
            _factory, $"member-{Guid.NewGuid()}@{domain}", _organization.Id);
        return (member, domain);
    }

    private static OrganizationUserUpdateRequestModel UpdateRequest(string? email = null, string? name = null,
        bool accessPam = false, OrganizationUserType type = OrganizationUserType.User) =>
        new()
        {
            Type = type,
            Permissions = new Permissions(),
            Collections = [],
            Groups = [],
            Email = email,
            Name = name,
            AccessPam = accessPam
        };

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response, IValidationError expectedError)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;
        Assert.Equal("validation_error", root.GetProperty("type").GetString());
        Assert.Equal("One or more validation errors occurred.", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());

        var errors = root.GetProperty("errors").GetProperty(expectedError.PropertyName);
        Assert.Equal(1, errors.GetArrayLength());
        Assert.Equal(expectedError.Type, errors[0].GetProperty("type").GetString());
        Assert.Equal(expectedError.Message, errors[0].GetProperty("detail").GetString());
    }
}
