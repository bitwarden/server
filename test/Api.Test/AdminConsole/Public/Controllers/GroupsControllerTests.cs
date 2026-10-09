using Bit.Api.AdminConsole.Public.Controllers;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Public.Controllers;

[ControllerCustomize(typeof(GroupsController))]
[SutProviderCustomize]
public class GroupsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Post_Success(Organization organization, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Contains at least one can manage
        groupRequestModel.Collections.First().Manage = true;

        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var response = await sutProvider.Sut.Post(groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.ExternalId == groupRequestModel.ExternalId),
            organization,
            Arg.Any<ICollection<CollectionAccessSelection>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success(Organization organization, Group group, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Contains at least one can manage
        groupRequestModel.Collections.First().Manage = true;

        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);

        var response = await sutProvider.Sut.Put(group.Id, groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.ExternalId == groupRequestModel.ExternalId),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<ICollection<CollectionAccessSelection>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task PutMemberIds_WhenMemberChangeIsAllowed_UpdatesMembers(Group group, UpdateMemberIdsRequestModel model,
        SutProvider<GroupsController> sutProvider)
    {
        ArrangeGroup(sutProvider, group);

        await sutProvider.Sut.PutMemberIds(group.Id, model);

        await sutProvider.GetDependency<IScopedApiKeyGroupMemberValidator>().Received(1)
            .ValidateAsync(group.OrganizationId, group.Id, model.MemberIds);
        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .UpdateUsersAsync(group.Id, model.MemberIds, Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutMemberIds_WhenScopedApiKeyCannotChangeMembers_ThrowsWithoutUpdating(Group group,
        UpdateMemberIdsRequestModel model, SutProvider<GroupsController> sutProvider)
    {
        ArrangeGroup(sutProvider, group);
        ArrangeScopedApiKeyRejection(sutProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutMemberIds(group.Id, model));

        Assert.Equal(new ScopedApiKeyCanOnlyManageUsers().Message, exception.Message);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs()
            .UpdateUsersAsync(default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Delete_WhenMemberChangeIsAllowed_DeletesGroup(Group group, SutProvider<GroupsController> sutProvider)
    {
        ArrangeGroup(sutProvider, group);

        await sutProvider.Sut.Delete(group.Id);

        await sutProvider.GetDependency<IScopedApiKeyGroupMemberValidator>().Received(1)
            .ValidateAsync(group.OrganizationId, group.Id, Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()));
        await sutProvider.GetDependency<IGroupRepository>().Received(1).DeleteAsync(group);
    }

    [Theory]
    [BitAutoData]
    public async Task Delete_WhenScopedApiKeyCannotRemoveMembers_ThrowsWithoutDeleting(Group group,
        SutProvider<GroupsController> sutProvider)
    {
        ArrangeGroup(sutProvider, group);
        ArrangeScopedApiKeyRejection(sutProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Delete(group.Id));

        Assert.Equal(new ScopedApiKeyCanOnlyManageUsers().Message, exception.Message);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    private static void ArrangeGroup(SutProvider<GroupsController> sutProvider, Group group)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(group.OrganizationId);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
    }

    private static void ArrangeScopedApiKeyRejection(SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IScopedApiKeyGroupMemberValidator>()
            .ValidateAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(new ScopedApiKeyCanOnlyManageUsers());
    }
}
