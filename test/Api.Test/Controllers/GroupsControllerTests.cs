using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(GroupsController))]
[SutProviderCustomize]
public class GroupsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Post_Success(Organization organization, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).ReturnsForAnyArgs(organization);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Post(organization.Id.ToString(), groupRequestModel);

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
        sutProvider.GetDependency<ICreateGroupCommand>().Received(1).Validate(organization);
        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            organization,
            Arg.Any<IEnumerable<SelectionReadOnly>>());

        Assert.NotNull(response.Id);
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id.ToString(), response.OrganizationId);
        Assert.Equal(groupRequestModel.AccessAll, response.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, response.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_WithManageGroupsReturningFalse_ThrowsNotFound(Guid organizationId, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Post(organizationId.ToString(), groupRequestModel));

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organizationId);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(organizationId);
        sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs().Validate(default);
        await sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs()
            .CreateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_WithValidateThrowBadRequest_ThrowsBadRequest(Organization organization, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).ReturnsForAnyArgs(organization);
        sutProvider.GetDependency<ICreateGroupCommand>().When(cgc => cgc.Validate(organization)).Do(_ => throw new BadRequestException());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Post(organization.Id.ToString(), groupRequestModel));

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
        sutProvider.GetDependency<ICreateGroupCommand>().Received(1).Validate(organization);
        await sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs()
            .CreateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success(Organization organization, Group group, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).ReturnsForAnyArgs(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).ReturnsForAnyArgs(group);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Put(organization.Id.ToString(), group.Id.ToString(), groupRequestModel);

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
        sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).Validate(organization);
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            Arg.Any<IEnumerable<SelectionReadOnly>>());

        Assert.NotNull(response.Id);
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id.ToString(), response.OrganizationId);
        Assert.Equal(groupRequestModel.AccessAll, response.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, response.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithManageGroupsReturningFalse_ThrowsNotFound(Organization organization, Group group, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).ReturnsForAnyArgs(group);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(organization.Id.ToString(), group.Id.ToString(), groupRequestModel));

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(group.OrganizationId);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(organization.Id);
        sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs().Validate(default);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithValidateThrowBadRequest_ThrowsBadRequest(Organization organization, Group group, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).ReturnsForAnyArgs(group);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).ReturnsForAnyArgs(organization);
        sutProvider.GetDependency<IUpdateGroupCommand>().When(cgc => cgc.Validate(organization)).Do(_ => throw new BadRequestException());

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Put(organization.Id.ToString(), group.Id.ToString(), groupRequestModel));

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
        sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).Validate(organization);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default);
    }
}
