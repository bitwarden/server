using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Authorization.Groups;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(GroupsController))]
[SutProviderCustomize]
public class GroupsControllerTests
{

    [Theory]
    [BitAutoData]
    public async Task Get_GroupNotFound_ThrowsNotFound(Guid orgId, Guid id,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Get(orgId, id));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_OrgIdMismatch_ThrowsNotFound(Guid orgId, Group group,
        SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Get(orgId, group.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_Success(Group group, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);

        var result = await sutProvider.Sut.Get(group.OrganizationId, group.Id);

        Assert.Equal(group.Id, result.Id);
        Assert.Equal(group.OrganizationId, result.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task GetDetails_GroupNotFound_ThrowsNotFound(Guid orgId, Guid id,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdWithCollectionsAsync(id)
            .Returns((Tuple<Group, ICollection<CollectionAccessSelection>>)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetDetails(orgId, id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetDetails_OrgIdMismatch_ThrowsNotFound(Guid orgId, Group group,
        ICollection<CollectionAccessSelection> collections, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdWithCollectionsAsync(group.Id)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, collections));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetDetails(orgId, group.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetDetails_Success(Group group, ICollection<CollectionAccessSelection> collections,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdWithCollectionsAsync(group.Id)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, collections));

        var result = await sutProvider.Sut.GetDetails(group.OrganizationId, group.Id);

        Assert.Equal(group.Id, result.Id);
        Assert.Equal(group.OrganizationId, result.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsers_GroupNotFound_ThrowsNotFound(Guid orgId, Guid id,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetUsers(orgId, id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsers_OrgIdMismatch_ThrowsNotFound(Guid orgId, Group group,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetUsers(orgId, group.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsers_Success(Group group, ICollection<Guid> userIds,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
        sutProvider.GetDependency<IGroupRepository>().GetManyUserIdsByIdAsync(group.Id).Returns(userIds);

        var result = await sutProvider.Sut.GetUsers(group.OrganizationId, group.Id);

        Assert.Equal(userIds, result);
    }

    [Theory]
    [BitAutoData]
    public async Task Delete_GroupNotFound_ThrowsNotFound(Guid orgId, Guid id,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Delete(orgId, id));
    }

    [Theory]
    [BitAutoData]
    public async Task Delete_OrgIdMismatch_ThrowsNotFound(Guid orgId, Group group,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Delete(orgId, group.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task Delete_Success(Group group, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);

        await sutProvider.Sut.Delete(group.OrganizationId, group.Id);

        await sutProvider.GetDependency<IDeleteGroupCommand>().Received(1).DeleteAsync(group);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_GroupNotFound_ThrowsNotFound(Guid orgId, Guid id, Guid orgUserId,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteUser(orgId, id, orgUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_OrgIdMismatch_ThrowsNotFound(Guid orgId, Group group, Guid orgUserId,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteUser(orgId, group.Id, orgUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_Success(Group group, Guid orgUserId,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);

        await sutProvider.Sut.DeleteUser(group.OrganizationId, group.Id, orgUserId);

        await sutProvider.GetDependency<IGroupService>().Received(1).DeleteUserAsync(group, orgUserId);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_OrgIdMismatch_ThrowsNotFound(Guid orgId,
        SutProvider<GroupsController> sutProvider)
    {
        var groups = new List<Group>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() }
        };
        var model = new GroupBulkRequestModel { Ids = groups.Select(g => g.Id) };
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(model.Ids).Returns(groups);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDelete(orgId, model));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_Success(Guid orgId, SutProvider<GroupsController> sutProvider)
    {
        var groups = new List<Group>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = orgId },
            new() { Id = Guid.NewGuid(), OrganizationId = orgId }
        };
        var model = new GroupBulkRequestModel { Ids = groups.Select(g => g.Id) };
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(model.Ids).Returns(groups);

        await sutProvider.Sut.BulkDelete(orgId, model);

        await sutProvider.GetDependency<IDeleteGroupCommand>().Received(1).DeleteManyAsync(groups);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_AuthorizedToGiveAccessToCollections_Success(Organization organization,
        GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Enable FC
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(
            new OrganizationAbility { Id = organization.Id, AllowAdminAccessToAllCollectionItems = false });

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                 Arg.Any<IEnumerable<Collection>>(),
                 Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
             .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var response = await sutProvider.Sut.Post(organization.Id, groupRequestModel);

        var requestModelCollectionIds = groupRequestModel.Collections.Select(c => c.Id).ToHashSet();

        // Assert that it checked permissions
        await sutProvider.GetDependency<IAuthorizationService>()
            .Received(1)
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Is<IEnumerable<Collection>>(collections =>
                    collections.All(c => requestModelCollectionIds.Contains(c.Id))),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Single() == BulkCollectionOperations.ModifyGroupAccess));

        // Assert that it saved the data
        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name),
            organization,
            Arg.Is<ICollection<CollectionAccessSelection>>(access =>
                access.All(c => requestModelCollectionIds.Contains(c.Id))),
            Arg.Any<IEnumerable<Guid>>());
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_NotAuthorizedToGiveAccessToCollections_Throws(Organization organization, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Enable FC
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(
            new OrganizationAbility { Id = organization.Id, AllowAdminAccessToAllCollectionItems = false });

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var requestModelCollectionIds = groupRequestModel.Collections.Select(c => c.Id).ToHashSet();
        sutProvider.GetDependency<IAuthorizationService>()
           .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Is<IEnumerable<Collection>>(collections => collections.All(c => requestModelCollectionIds.Contains(c.Id))),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Post(organization.Id, groupRequestModel));

        await sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs()
            .CreateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_GroupNotFound_ThrowsNotFound(Guid orgId, Guid id, GroupRequestModel model,
        SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdWithCollectionsAsync(id)
            .Returns(new Tuple<Group?, ICollection<CollectionAccessSelection>>(null, new List<CollectionAccessSelection>()));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(orgId, id, model));

        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_AddingSelfToGroup_WhenAdminAccessDisabled_ThrowsBadRequest(Organization organization,
        Group group, GroupRequestModel model, OrganizationUser callerOrganizationUser, Guid userId,
        SutProvider<GroupsController> sutProvider)
    {
        ArrangePut(sutProvider, organization, group, model, allowAdminAccessToAllCollectionItems: false);
        model.Users = [callerOrganizationUser.Id];
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, userId).Returns(callerOrganizationUser);
        sutProvider.GetDependency<IGroupRepository>().GetManyUserIdsByIdAsync(group.Id).Returns(new List<Guid>());

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.Put(organization.Id, group.Id, model));

        Assert.Equal("You cannot add yourself to groups.", exception.Message);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_NotAuthorizedForPostedCollection_ThrowsNotFound(Organization organization, Group group,
        GroupRequestModel model, SutProvider<GroupsController> sutProvider)
    {
        ArrangePut(sutProvider, organization, group, model, allowAdminAccessToAllCollectionItems: true);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Collection>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(organization.Id, group.Id, model));

        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_PreservesCurrentCollectionsTheCallerCannotEdit(Organization organization, Group group,
        GroupRequestModel model, SutProvider<GroupsController> sutProvider)
    {
        var readonlyCollection = new Collection { Id = Guid.NewGuid(), OrganizationId = organization.Id };
        var currentAccess = new List<CollectionAccessSelection>
        {
            new() { Id = readonlyCollection.Id, Manage = true },
        };
        ArrangePut(sutProvider, organization, group, model, allowAdminAccessToAllCollectionItems: true, currentAccess);

        var postedCollections = model.Collections
            .Select(c => new Collection { Id = c.Id, OrganizationId = organization.Id }).ToList();
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(readonlyCollection.Id)))
            .Returns(new List<Collection> { readonlyCollection });
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Contains(readonlyCollection.Id)))
            .Returns(postedCollections);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Collection>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Success());
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), readonlyCollection,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Failed());

        await sutProvider.Sut.Put(organization.Id, group.Id, model);

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Any<Group>(), organization,
            Arg.Is<ICollection<CollectionAccessSelection>>(access => access.Any(a => a.Id == readonlyCollection.Id)),
            Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithNewAuthorizationEnabled_PreservesUnauthorizedCurrentCollections(
        Organization organization, Group group, GroupRequestModel model, Guid readonlyCollectionId,
        SutProvider<GroupsController> sutProvider)
    {
        var currentAccess = new List<CollectionAccessSelection>
        {
            new() { Id = readonlyCollectionId, Manage = true },
        };
        ArrangePut(sutProvider, organization, group, model, allowAdminAccessToAllCollectionItems: true, currentAccess);
        EnableNewAuthorization(sutProvider);
        var postedCollectionIds = model.Collections.Select(c => c.Id).ToList();
        sutProvider.GetDependency<IGroupsAuthorizationService>()
            .AuthorizeSaveAsync(organization.Id, group.Id,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(postedCollectionIds)),
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { readonlyCollectionId })),
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(model.Users)))
            .Returns(new GroupsAuthorizationResult(true, new HashSet<Guid>(), new HashSet<Guid> { readonlyCollectionId }));

        await sutProvider.Sut.Put(organization.Id, group.Id, model);

        // The posted collections are saved, and the collection the caller cannot change is kept.
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Any<Group>(), organization,
            Arg.Is<ICollection<CollectionAccessSelection>>(access => access.Select(a => a.Id).OrderBy(id => id)
                .SequenceEqual(postedCollectionIds.Append(readonlyCollectionId).OrderBy(id => id))),
            Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IAuthorizationService>().DidNotReceiveWithAnyArgs()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Collection>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>());
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithNewAuthorizationEnabled_CannotAddSelfToGroup_ThrowsBadRequest(Organization organization,
        Group group, GroupRequestModel model, SutProvider<GroupsController> sutProvider)
    {
        ArrangePut(sutProvider, organization, group, model, allowAdminAccessToAllCollectionItems: false);
        EnableNewAuthorization(sutProvider);
        // Match on the posted members, so that the rule stays covered if the controller stops passing them.
        sutProvider.GetDependency<IGroupsAuthorizationService>()
            .AuthorizeSaveAsync(organization.Id, group.Id, Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(model.Users)))
            .Returns(new GroupsAuthorizationResult(false, new HashSet<Guid>(), new HashSet<Guid>()));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.Put(organization.Id, group.Id, model));

        Assert.Equal("You cannot add yourself to groups.", exception.Message);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithNewAuthorizationEnabled_UnauthorizedPostedCollection_ThrowsNotFound(
        Organization organization, Group group, GroupRequestModel model, Guid unauthorizedCollectionId,
        SutProvider<GroupsController> sutProvider)
    {
        ArrangePut(sutProvider, organization, group, model, allowAdminAccessToAllCollectionItems: true);
        EnableNewAuthorization(sutProvider);
        var postedCollectionIds = model.Collections.Select(c => c.Id).ToList();
        sutProvider.GetDependency<IGroupsAuthorizationService>()
            .AuthorizeSaveAsync(organization.Id, group.Id,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(postedCollectionIds)),
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new GroupsAuthorizationResult(true, new HashSet<Guid> { unauthorizedCollectionId },
                new HashSet<Guid>()));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(organization.Id, group.Id, model));

        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_WithNewAuthorizationEnabled_Success(Organization organization,
        GroupRequestModel model, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        EnableNewAuthorization(sutProvider);
        var postedCollectionIds = model.Collections.Select(c => c.Id).ToList();
        sutProvider.GetDependency<IGroupsAuthorizationService>()
            .AuthorizeSaveAsync(organization.Id, null,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(postedCollectionIds)),
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0),
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(model.Users)))
            .Returns(new GroupsAuthorizationResult(true, new HashSet<Guid>(), new HashSet<Guid>()));

        await sutProvider.Sut.Post(organization.Id, model);

        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g => g.OrganizationId == organization.Id && g.Name == model.Name),
            organization,
            Arg.Is<ICollection<CollectionAccessSelection>>(access =>
                access.Select(a => a.Id).SequenceEqual(postedCollectionIds)),
            Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IAuthorizationService>().DidNotReceiveWithAnyArgs()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<IEnumerable<Collection>>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>());
    }

    [Theory]
    [BitAutoData]
    public async Task Post_WithNewAuthorizationEnabled_UnauthorizedPostedCollection_ThrowsNotFound(
        Organization organization, GroupRequestModel model, Guid unauthorizedCollectionId,
        SutProvider<GroupsController> sutProvider)
    {
        EnableNewAuthorization(sutProvider);
        sutProvider.GetDependency<IGroupsAuthorizationService>()
            .AuthorizeSaveAsync(organization.Id, null, Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new GroupsAuthorizationResult(true, new HashSet<Guid> { unauthorizedCollectionId },
                new HashSet<Guid>()));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Post(organization.Id, model));

        await sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs()
            .CreateGroupAsync(default, default, default, default);
    }

    private static void EnableNewAuthorization(SutProvider<GroupsController> sutProvider) =>
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AuthorizationServices)
            .Returns(true);

    private static void ArrangePut(SutProvider<GroupsController> sutProvider, Organization organization, Group group,
        GroupRequestModel model, bool allowAdminAccessToAllCollectionItems,
        ICollection<CollectionAccessSelection>? currentAccess = null)
    {
        group.OrganizationId = organization.Id;
        sutProvider.GetDependency<IGroupRepository>().GetByIdWithCollectionsAsync(group.Id)
            .Returns(new Tuple<Group?, ICollection<CollectionAccessSelection>>(
                group, currentAccess ?? new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                AllowAdminAccessToAllCollectionItems = allowAdminAccessToAllCollectionItems,
            });
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(model.Collections.Select(c => new Collection { Id = c.Id, OrganizationId = organization.Id })
                .ToList());
    }
}
