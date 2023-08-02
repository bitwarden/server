using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Core.Context;
using Bit.Core.Entities;
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
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Post(organization.Id.ToString(), groupRequestModel);

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll),
            organization,
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<Guid>>());
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
        Assert.Equal(groupRequestModel.AccessAll, response.AccessAll);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success(Organization organization, Group group, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Put(organization.Id.ToString(), group.Id.ToString(), groupRequestModel);

        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<Guid>>());
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
        Assert.Equal(groupRequestModel.AccessAll, response.AccessAll);
    }
}
