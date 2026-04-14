using System.Security.Claims;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
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
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(
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
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(
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
    public async Task Post_FeatureFlagEnabled_UsesCollectionGroupOperations(Organization organization,
        GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(
            new OrganizationAbility { Id = organization.Id, AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(true);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                 Arg.Any<IEnumerable<Collection>>(),
                 Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(CollectionGroupOperations.Create)))
             .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Post(organization.Id, groupRequestModel);

        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Any<Group>(), organization, Arg.Any<ICollection<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [BitAutoData]
    public async Task Post_FeatureFlagEnabled_NotAuthorized_Throws(Organization organization,
        GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(
            new OrganizationAbility { Id = organization.Id, AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(CollectionGroupOperations.Create)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Post(organization.Id, groupRequestModel));
    }
}
