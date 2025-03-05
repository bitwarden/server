using System.Security.Claims;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

#nullable enable

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(GroupsController))]
[SutProviderCustomize]
public class GroupsControllerPutTests
{
    [Theory]
    [BitAutoData]
    public async Task Put_WithAdminAccess_Success(Organization organization, Group group,
        GroupRequestModel groupRequestModel, List<CollectionAccessSelection> existingCollectionAccess,
        OrganizationUser savingUser, SutProvider<GroupsController> sutProvider)
    {
        Put_Setup(sutProvider, organization, true, group, savingUser, existingCollectionAccess, []);

        var requestModelCollectionIds = groupRequestModel.Collections.Select(c => c.Id).ToHashSet();

        // Authorize all changes for basic happy path test
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Collection>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
            .Returns(AuthorizationResult.Success());

        var response = await sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel);

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            // Should overwrite any existing collections
            Arg.Is<ICollection<CollectionAccessSelection>>(access =>
                access.All(c => requestModelCollectionIds.Contains(c.Id))),
            Arg.Is<IEnumerable<Guid>>(guids => guids.ToHashSet().SetEquals(groupRequestModel.Users.ToHashSet())));
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateMembers_NoAdminAccess_CannotAddSelfToGroup(Organization organization, Group group,
        GroupRequestModel groupRequestModel, OrganizationUser savingUser, List<Guid> currentGroupUsers,
        SutProvider<GroupsController> sutProvider)
    {
        // Not updating collections
        groupRequestModel.Collections = [];

        Put_Setup(sutProvider, organization, false, group, savingUser,
            currentCollectionAccess: [], currentGroupUsers);

        // Saving user is trying to add themselves to the group
        var updatedUsers = groupRequestModel.Users.ToList();
        updatedUsers.Add(savingUser.Id);
        groupRequestModel.Users = updatedUsers;

        var exception = await
            Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel));

        Assert.Contains("You cannot add yourself to groups", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateMembers_NoAdminAccess_AlreadyInGroup_Success(Organization organization, Group group,
        GroupRequestModel groupRequestModel, OrganizationUser savingUser, List<Guid> currentGroupUsers,
        SutProvider<GroupsController> sutProvider)
    {
        // Not changing collection access
        groupRequestModel.Collections = [];

        // Saving user is trying to add themselves to the group
        var updatedUsers = groupRequestModel.Users.ToList();
        updatedUsers.Add(savingUser.Id);
        groupRequestModel.Users = updatedUsers;

        // But! they are already a member of the group
        currentGroupUsers.Add(savingUser.Id);

        Put_Setup(sutProvider, organization, false, group, savingUser, currentCollectionAccess: [], currentGroupUsers);

        var response = await sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel);

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<ICollection<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<Guid>>());
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateMembers_WithAdminAccess_CanAddSelfToGroup(Organization organization, Group group,
        GroupRequestModel groupRequestModel, OrganizationUser savingUser, List<Guid> currentGroupUsers,
        SutProvider<GroupsController> sutProvider)
    {
        // Not updating collections
        groupRequestModel.Collections = [];

        Put_Setup(sutProvider, organization, true, group, savingUser,
            currentCollectionAccess: [], currentGroupUsers);

        // Saving user is trying to add themselves to the group
        var updatedUsers = groupRequestModel.Users.ToList();
        updatedUsers.Add(savingUser.Id);
        groupRequestModel.Users = updatedUsers;

        var response = await sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel);

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<ICollection<CollectionAccessSelection>>(),
            Arg.Is<IEnumerable<Guid>>(guids => guids.ToHashSet().SetEquals(groupRequestModel.Users.ToHashSet())));
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateMembers_NoAdminAccess_ProviderUser_Success(Organization organization, Group group,
        GroupRequestModel groupRequestModel, List<Guid> currentGroupUsers, SutProvider<GroupsController> sutProvider)
    {
        // Make collection authorization pass, it's not being tested here
        groupRequestModel.Collections = Array.Empty<SelectionReadOnlyRequestModel>();

        Put_Setup(sutProvider, organization, false, group, null, currentCollectionAccess: [], currentGroupUsers);

        var response = await sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel);

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<ICollection<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<Guid>>());
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateCollections_DoesNotOverwriteUnauthorizedCollections(GroupRequestModel groupRequestModel,
        Group group, Organization organization,
        SutProvider<GroupsController> sutProvider, OrganizationUser savingUser)
    {
        var editedCollectionId = CoreHelpers.GenerateComb();
        var readonlyCollectionId1 = CoreHelpers.GenerateComb();
        var readonlyCollectionId2 = CoreHelpers.GenerateComb();

        var currentCollectionAccess = new List<CollectionAccessSelection>
        {
            new()
            {
                Id = editedCollectionId,
                HidePasswords = true,
                Manage = false,
                ReadOnly = true
            },
            new()
            {
                Id = readonlyCollectionId1,
                HidePasswords = false,
                Manage = true,
                ReadOnly = false
            },
            new()
            {
                Id = readonlyCollectionId2,
                HidePasswords = false,
                Manage = false,
                ReadOnly = false
            },
        };

        Put_Setup(sutProvider, organization, false, group, savingUser, currentCollectionAccess, currentGroupUsers: []);

        // User is upgrading editedCollectionId to manage
        groupRequestModel.Collections = new List<SelectionReadOnlyRequestModel>
        {
            new() { Id = editedCollectionId, HidePasswords = false, Manage = true, ReadOnly = false }
        };

        // Authorize the editedCollection
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => c.Id == editedCollectionId),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
            .Returns(AuthorizationResult.Success());

        // Do not authorize the readonly collections
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => c.Id == readonlyCollectionId1 || c.Id == readonlyCollectionId2),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
            .Returns(AuthorizationResult.Failed());

        var response = await sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel);

        // Expect all collection access (modified and unmodified) to be saved
        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Is<List<CollectionAccessSelection>>(cas =>
                cas.Select(c => c.Id).SequenceEqual(currentCollectionAccess.Select(c => c.Id)) &&
                cas.First(c => c.Id == editedCollectionId).Manage == true &&
                cas.First(c => c.Id == editedCollectionId).ReadOnly == false &&
                cas.First(c => c.Id == editedCollectionId).HidePasswords == false),
            Arg.Any<IEnumerable<Guid>>());
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateCollections_ThrowsIfSavingUserCannotUpdateCollections(GroupRequestModel groupRequestModel,
        Group group, Organization organization,
        SutProvider<GroupsController> sutProvider, OrganizationUser savingUser)
    {
        // Group is currently assigned to the POSTed collections
        Put_Setup(sutProvider, organization, false, group, savingUser,
            groupRequestModel.Collections.Select(cas => cas.ToSelectionReadOnly()).ToList(),
            []);

        var postedCollectionIds = groupRequestModel.Collections.Select(c => c.Id).ToHashSet();

        // But the saving user does not have permission to update them
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => postedCollectionIds.Contains(c.Id)),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateCollections_ThrowsIfSavingUserCannotAddCollections(GroupRequestModel groupRequestModel,
        Group group, Organization organization,
        SutProvider<GroupsController> sutProvider, OrganizationUser savingUser)
    {
        // Group is not assigned to the POSTed collections
        Put_Setup(sutProvider, organization, false, group, savingUser, [], []);

        var postedCollectionIds = groupRequestModel.Collections.Select(c => c.Id).ToHashSet();

        // But the saving user does not have permission to update them
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => postedCollectionIds.Contains(c.Id)),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel));
    }

    private void Put_Setup(SutProvider<GroupsController> sutProvider, Organization organization,
        bool adminAccess, Group group, OrganizationUser? savingUser, List<CollectionAccessSelection> currentCollectionAccess,
        List<Guid> currentGroupUsers)
    {
        var orgId = organization.Id = group.OrganizationId;

        // Arrange org and orgAbility
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                AllowAdminAccessToAllCollectionItems = adminAccess
            });

        // Arrange user
        // If no savingUser provided, they're not an org user, just return a random guid
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(savingUser?.UserId ?? CoreHelpers.GenerateComb());
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(orgId).Returns(true);

        // Arrange repositories
        sutProvider.GetDependency<IGroupRepository>().GetManyUserIdsByIdAsync(group.Id).Returns(currentGroupUsers ?? []);
        sutProvider.GetDependency<IGroupRepository>().GetByIdWithCollectionsAsync(group.Id)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, currentCollectionAccess ?? []));
        if (savingUser != null)
        {
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgId, savingUser.UserId!.Value)
                .Returns(savingUser);
        }

        // Collection repository: return mock Collection objects for any ids passed in
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>().Select(guid => new Collection { Id = guid }).ToList());
    }
}
