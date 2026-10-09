using System.Net;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Public.Controllers;

public class ScopedOrganizationApiKeyGroupMembershipTests
    : IClassFixture<ScopedOrganizationApiKeysApiApplicationFactory>, IAsyncLifetime
{
    private readonly ScopedOrganizationApiKeysApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private OrganizationUser _owner = null!;
    private OrganizationUser _admin = null!;
    private OrganizationUser _custom = null!;
    private OrganizationUser _user = null!;
    private Group _group = null!;

    public ScopedOrganizationApiKeyGroupMembershipTests(ScopedOrganizationApiKeysApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        (_organization, _owner) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        _admin = await CreateMemberAsync(OrganizationUserType.Admin);
        _custom = await CreateMemberAsync(OrganizationUserType.Custom);
        _user = await CreateMemberAsync(OrganizationUserType.User);
        _group = await CreateGroupAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task MembersWriteToken_AddElevatedMemberToGroup_IsRejectedAndGroupsUnchanged(OrganizationUserType type)
    {
        var member = MemberOfType(type);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersWrite);

        var response = await _client.PutAsJsonAsync($"public/members/{member.Id}/group-ids",
            new UpdateGroupIdsRequestModel { GroupIds = [_group.Id] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new ScopedApiKeyCanOnlyManageUsers().Message, await response.Content.ReadAsStringAsync());
        Assert.Empty(await GetGroupIdsAsync(member.Id));
    }

    [Fact]
    public async Task MembersWriteToken_RemoveAdminFromGroup_IsRejectedAndAdminStaysInGroup()
    {
        await SetGroupMembersAsync(_group, _admin.Id);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersWrite);

        var response = await _client.PutAsJsonAsync($"public/members/{_admin.Id}/group-ids",
            new UpdateGroupIdsRequestModel { GroupIds = [] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new ScopedApiKeyCanOnlyManageUsers().Message, await response.Content.ReadAsStringAsync());
        Assert.Equal(_group.Id, Assert.Single(await GetGroupIdsAsync(_admin.Id)));
    }

    [Fact]
    public async Task MembersWriteToken_AddUserToGroup_AddsUser()
    {
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersWrite);

        var response = await _client.PutAsJsonAsync($"public/members/{_user.Id}/group-ids",
            new UpdateGroupIdsRequestModel { GroupIds = [_group.Id] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_group.Id, Assert.Single(await GetGroupIdsAsync(_user.Id)));
    }

    [Fact]
    public async Task LegacyToken_AddAdminToGroup_AddsAdmin()
    {
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);

        var response = await _client.PutAsJsonAsync($"public/members/{_admin.Id}/group-ids",
            new UpdateGroupIdsRequestModel { GroupIds = [_group.Id] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_group.Id, Assert.Single(await GetGroupIdsAsync(_admin.Id)));
    }

    [Fact]
    public async Task GroupsWriteToken_PutMemberIdsAddingAdmin_IsRejectedAndGroupUnchanged()
    {
        await SetGroupMembersAsync(_group, _user.Id);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationGroupsWrite);

        var response = await _client.PutAsJsonAsync($"public/groups/{_group.Id}/member-ids",
            new UpdateMemberIdsRequestModel { MemberIds = [_user.Id, _admin.Id] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new ScopedApiKeyCanOnlyManageUsers().Message, await response.Content.ReadAsStringAsync());
        AssertSameMembers([_user.Id], await GetMemberIdsAsync(_group.Id));
    }

    [Fact]
    public async Task GroupsWriteToken_PutMemberIdsRemovingAdmin_IsRejectedAndGroupUnchanged()
    {
        await SetGroupMembersAsync(_group, _admin.Id, _user.Id);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationGroupsWrite);

        var response = await _client.PutAsJsonAsync($"public/groups/{_group.Id}/member-ids",
            new UpdateMemberIdsRequestModel { MemberIds = [_user.Id] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new ScopedApiKeyCanOnlyManageUsers().Message, await response.Content.ReadAsStringAsync());
        AssertSameMembers([_admin.Id, _user.Id], await GetMemberIdsAsync(_group.Id));
    }

    [Fact]
    public async Task GroupsWriteToken_PutMemberIdsKeepingAdminAndAddingUser_AddsUser()
    {
        await SetGroupMembersAsync(_group, _admin.Id, _user.Id);
        var user2 = await CreateMemberAsync(OrganizationUserType.User);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationGroupsWrite);

        var response = await _client.PutAsJsonAsync($"public/groups/{_group.Id}/member-ids",
            new UpdateMemberIdsRequestModel { MemberIds = [_admin.Id, _user.Id, user2.Id] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSameMembers([_admin.Id, _user.Id, user2.Id], await GetMemberIdsAsync(_group.Id));
    }

    [Fact]
    public async Task LegacyToken_PutMemberIdsAddingAdmin_AddsAdmin()
    {
        await SetGroupMembersAsync(_group, _user.Id);
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);

        var response = await _client.PutAsJsonAsync($"public/groups/{_group.Id}/member-ids",
            new UpdateMemberIdsRequestModel { MemberIds = [_user.Id, _admin.Id] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSameMembers([_user.Id, _admin.Id], await GetMemberIdsAsync(_group.Id));
    }

    [Fact]
    public async Task GroupsWriteToken_DeleteGroupContainingAdmin_IsRejectedAndGroupRemains()
    {
        await SetGroupMembersAsync(_group, _admin.Id);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationGroupsWrite);

        var response = await _client.DeleteAsync($"public/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new ScopedApiKeyCanOnlyManageUsers().Message, await response.Content.ReadAsStringAsync());
        Assert.NotNull(await _factory.GetService<IGroupRepository>().GetByIdAsync(_group.Id));
    }

    [Fact]
    public async Task GroupsWriteToken_DeleteGroupWithOnlyUsers_DeletesGroup()
    {
        await SetGroupMembersAsync(_group, _user.Id);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationGroupsWrite);

        var response = await _client.DeleteAsync($"public/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await _factory.GetService<IGroupRepository>().GetByIdAsync(_group.Id));
    }

    [Fact]
    public async Task LegacyToken_DeleteGroupContainingAdmin_DeletesGroup()
    {
        await SetGroupMembersAsync(_group, _admin.Id);
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);

        var response = await _client.DeleteAsync($"public/groups/{_group.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await _factory.GetService<IGroupRepository>().GetByIdAsync(_group.Id));
    }

    [Fact]
    public async Task MembersAndGroupsWriteToken_ImportGroups_LeavesAdminGroupMembershipsAlone()
    {
        await SetGroupMembersAsync(_group, _admin.Id);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id,
            ApiScopes.ApiOrganizationMembersWrite, ApiScopes.ApiOrganizationGroupsWrite);
        var import = GroupSyncImport();

        var response = await _client.PostAsJsonAsync("public/organization/import", import);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSameMembers([_admin.Id, _user.Id], await GetMemberIdsAsync(_group.Id));
        AssertSameMembers([_user.Id], await GetMemberIdsAsync(await GetGroupIdByExternalIdAsync(import.Groups[1].ExternalId)));
    }

    [Fact]
    public async Task LegacyToken_ImportGroups_SyncsAdminGroupMemberships()
    {
        await SetGroupMembersAsync(_group, _admin.Id);
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);
        var import = GroupSyncImport();

        var response = await _client.PostAsJsonAsync("public/organization/import", import);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSameMembers([_user.Id], await GetMemberIdsAsync(_group.Id));
        AssertSameMembers([_admin.Id, _user.Id],
            await GetMemberIdsAsync(await GetGroupIdByExternalIdAsync(import.Groups[1].ExternalId)));
    }

    private OrganizationUser MemberOfType(OrganizationUserType type) => type switch
    {
        OrganizationUserType.Owner => _owner,
        OrganizationUserType.Admin => _admin,
        OrganizationUserType.Custom => _custom,
        OrganizationUserType.User => _user,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private async Task<OrganizationUser> CreateMemberAsync(OrganizationUserType type)
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        return await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email, type,
            externalId: $"external-{Guid.NewGuid()}");
    }

    private async Task<Group> CreateGroupAsync()
    {
        var group = new Group
        {
            OrganizationId = _organization.Id,
            Name = "G",
            ExternalId = $"external-{Guid.NewGuid()}",
        };
        await _factory.GetService<IGroupRepository>().CreateAsync(group, new List<CollectionAccessSelection>());
        return group;
    }

    private Task SetGroupMembersAsync(Group group, params Guid[] memberIds) =>
        _factory.GetService<IGroupRepository>().UpdateUsersAsync(group.Id, memberIds, DateTime.UtcNow);

    private async Task<ICollection<Guid>> GetGroupIdsAsync(Guid memberId) =>
        await _factory.GetService<IGroupRepository>().GetManyIdsByUserIdAsync(memberId);

    private async Task<ICollection<Guid>> GetMemberIdsAsync(Guid groupId) =>
        await _factory.GetService<IGroupRepository>().GetManyUserIdsByIdAsync(groupId);

    private async Task<Guid> GetGroupIdByExternalIdAsync(string externalId) =>
        Assert.Single(await _factory.GetService<IGroupRepository>().GetManyByOrganizationIdAsync(_organization.Id),
            g => g.ExternalId == externalId).Id;

    private static void AssertSameMembers(IEnumerable<Guid> expected, IEnumerable<Guid> actual) =>
        Assert.Equal(expected.Order(), actual.Order());

    private OrganizationImportRequestModel GroupSyncImport() => new()
    {
        Groups =
        [
            new OrganizationImportRequestModel.OrganizationImportGroupRequestModel
            {
                Name = _group.Name,
                ExternalId = _group.ExternalId,
                MemberExternalIds = [_user.ExternalId],
            },
            new OrganizationImportRequestModel.OrganizationImportGroupRequestModel
            {
                Name = "H",
                ExternalId = $"external-{Guid.NewGuid()}",
                MemberExternalIds = [_admin.ExternalId, _user.ExternalId],
            },
        ],
        Members =
        [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = _admin.Email,
                ExternalId = _admin.ExternalId,
            },
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = _user.Email,
                ExternalId = _user.ExternalId,
            },
        ],
        OverwriteExisting = false,
    };
}
