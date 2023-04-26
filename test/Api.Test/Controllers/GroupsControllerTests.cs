using System.Security.Claims;
using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.AuthorizationHandlers;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
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

        var response = await sutProvider.Sut.Post(organization.Id, groupRequestModel);

        var expectedGroup = () => Arg.Is<Group>(g =>
            g.OrganizationId == organization.Id &&
            g.Name == groupRequestModel.Name &&
            g.AccessAll == groupRequestModel.AccessAll);

        sutProvider.GetDependency<IBitAuthorizationService>()
            .Received(1)
            .AuthorizeOrThrowAsync(Arg.Any<ClaimsPrincipal>(), expectedGroup(), GroupOperations.Create);
        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            expectedGroup(),
            organization,
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<Guid>>());

        Assert.NotNull(response.Id);
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id.ToString(), response.OrganizationId);
        Assert.Equal(groupRequestModel.AccessAll, response.AccessAll);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success(Organization organization, Group group, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);

        var response = await sutProvider.Sut.Put(organization.Id, group.Id, groupRequestModel);

        sutProvider.GetDependency<IBitAuthorizationService>()
            .Received(1)
            .AuthorizeOrThrowAsync(Arg.Any<ClaimsPrincipal>(), group, GroupOperations.Update);
        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<Guid>>());

        Assert.NotNull(response.Id);
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id.ToString(), response.OrganizationId);
        Assert.Equal(groupRequestModel.AccessAll, response.AccessAll);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteUser_Success(Organization organization, Group group, OrganizationUser organizationUser, SutProvider<GroupsController> sutProvider)
    {
        group.OrganizationId = organization.Id;
        organizationUser.OrganizationId = organization.Id;
        var groupUser = new GroupUser() { GroupId = group.Id, OrganizationUserId = organizationUser.Id };

        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
        sutProvider.GetDependency<IGroupRepository>().GetGroupUserByGroupIdOrganizationUserId(group.Id, organizationUser.Id).Returns(groupUser);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        await sutProvider.Sut.Delete(organization.Id, group.Id, organizationUser.Id);

        await sutProvider.GetDependency<IBitAuthorizationService>()
            .Received(1)
            .AuthorizeOrThrowAsync(Arg.Any<ClaimsPrincipal>(), groupUser, GroupUserOperations.Delete);
        sutProvider.GetDependency<IGroupRepository>()
            .Received(1)
            .DeleteUserAsync(group.Id, organizationUser.Id);
        sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }
}
