using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Api.Public.Controllers;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Public.Controllers;

[ControllerCustomize(typeof(GroupsController))]
[SutProviderCustomize]
public class GroupsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Post_Success(Organization organization, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).ReturnsForAnyArgs(organization);

        var response = await sutProvider.Sut.Post(groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
        sutProvider.GetDependency<ICreateGroupCommand>().Received(1).Validate(organization);
        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            organization,
            Arg.Any<IEnumerable<SelectionReadOnly>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.AccessAll, responseValue.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_WithNoCurrentContextOrganizationId_ThrowsInvalidOperation(Guid organizationId, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.Post(groupRequestModel));

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(organizationId);
        sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs().Validate(default);
        await sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs()
            .CreateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_WithValidateThrowBadRequest_ThrowsBadRequest(Organization organization, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).ReturnsForAnyArgs(organization);
        sutProvider.GetDependency<ICreateGroupCommand>().When(cgc => cgc.Validate(organization)).Do(_ => throw new BadRequestException());

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Post(groupRequestModel));

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
        sutProvider.GetDependency<ICreateGroupCommand>().Received(1).Validate(organization);
        await sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs()
            .CreateGroupAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success(Organization organization, Group group, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).ReturnsForAnyArgs(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).ReturnsForAnyArgs(group);
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);

        var response = await sutProvider.Sut.Put(group.Id, groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
        sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).Validate(organization);
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            Arg.Any<IEnumerable<SelectionReadOnly>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.AccessAll, responseValue.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithNoCurrentContextOrganizationId_ThrowsInvalidOperation(Group group, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).ReturnsForAnyArgs(group);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.Post(groupRequestModel));

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs().Validate(default);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithValidateThrowBadRequest_ThrowsBadRequest(Organization organization, Group group, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).ReturnsForAnyArgs(group);
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).ReturnsForAnyArgs(organization);
        sutProvider.GetDependency<IUpdateGroupCommand>().When(cgc => cgc.Validate(organization)).Do(_ => throw new BadRequestException());

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Put(group.Id, groupRequestModel));

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdAsync(organization.Id);
        sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).Validate(organization);
        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs()
            .UpdateGroupAsync(default, default, default);
    }
}
