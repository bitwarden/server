using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Api.Public.Controllers;
using Bit.Core.Context;
using Bit.Core.Entities;
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
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var response = await sutProvider.Sut.Post(groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            organization,
            Arg.Any<IEnumerable<CollectionAccessSelection>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.AccessAll, responseValue.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success(Organization organization, Group group, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);

        var response = await sutProvider.Sut.Put(group.Id, groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<IEnumerable<CollectionAccessSelection>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.AccessAll, responseValue.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }
}
