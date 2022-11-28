using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
[OrganizationCustomize(UseGroups = true)]
public class GroupServiceTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_ValidData_DeletesGroup(Group group, SutProvider<GroupService> sutProvider)
    {
        await sutProvider.Sut.DeleteAsync(group);

        await sutProvider.GetDependency<IGroupRepository>().Received().DeleteAsync(group);
        await sutProvider.GetDependency<IEventService>().Received().LogGroupEventAsync(group, EventType.Group_Deleted);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_ValidData_WithEventSystemUser_DeletesGroup(Group group, EventSystemUser eventSystemUser, SutProvider<GroupService> sutProvider)
    {
        await sutProvider.Sut.DeleteAsync(group, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received().DeleteAsync(group);
        await sutProvider.GetDependency<IEventService>().Received().LogGroupEventAsync(group, EventType.Group_Deleted, eventSystemUser);
    }

    [Theory, BitAutoData]
    public async Task DeleteUserAsync_ValidData_DeletesUserInGroupRepository(Group group, Organization organization, OrganizationUser organizationUser, SutProvider<GroupService> sutProvider)
    {
        group.OrganizationId = organization.Id;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        organizationUser.OrganizationId = organization.Id;
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        await sutProvider.Sut.DeleteUserAsync(group, organizationUser.Id);

        await sutProvider.GetDependency<IGroupRepository>().Received().DeleteUserAsync(group.Id, organizationUser.Id);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups);
    }

    [Theory, BitAutoData]
    public async Task DeleteUserAsync_ValidData_WithEventSystemUser_DeletesUserInGroupRepository(Group group, Organization organization, OrganizationUser organizationUser, EventSystemUser eventSystemUser, SutProvider<GroupService> sutProvider)
    {
        group.OrganizationId = organization.Id;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        organizationUser.OrganizationId = organization.Id;
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        await sutProvider.Sut.DeleteUserAsync(group, organizationUser.Id, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received().DeleteUserAsync(group.Id, organizationUser.Id);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_UpdatedGroups, eventSystemUser);
    }

    [Theory, BitAutoData]
    public async Task DeleteUserAsync_InvalidUser_ThrowsNotFound(Group group, Organization organization, OrganizationUser organizationUser, SutProvider<GroupService> sutProvider)
    {
        group.OrganizationId = organization.Id;
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        // organizationUser.OrganizationId = organization.Id;
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // user not in organization
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteUserAsync(group, organizationUser.Id));
        // invalid user
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteUserAsync(group, Guid.NewGuid()));
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs()
            .DeleteUserAsync(default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default, default);
    }
}
