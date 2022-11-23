using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
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
    public async Task SaveAsync_DefaultGroupId_CreatesGroupInRepository(Group group, Organization organization, SutProvider<GroupService> sutProvider)
    {
        group.Id = default(Guid);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        organization.UseGroups = true;
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(group);

        await sutProvider.GetDependency<IGroupRepository>().Received().CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogGroupEventAsync(group, EventType.Group_Created);
        Assert.True(group.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(group.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_DefaultGroupIdAndCollections_CreatesGroupInRepository(Group group, Organization organization, List<CollectionAccessSelection> collections, SutProvider<GroupService> sutProvider)
    {
        group.Id = default(Guid);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        organization.UseGroups = true;
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(group, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received().CreateAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogGroupEventAsync(group, EventType.Group_Created);
        Assert.True(group.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(group.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_NonDefaultGroupId_ReplaceGroupInRepository(Group group, Organization organization, List<CollectionAccessSelection> collections, SutProvider<GroupService> sutProvider)
    {
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        await sutProvider.Sut.SaveAsync(group, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received().ReplaceAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogGroupEventAsync(group, EventType.Group_Updated);
        Assert.True(group.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_NonDefaultGroupId_ReplaceGroupInRepository_NoCollections(Group group, Organization organization, SutProvider<GroupService> sutProvider)
    {
        organization.UseGroups = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        await sutProvider.Sut.SaveAsync(group, null);

        await sutProvider.GetDependency<IGroupRepository>().Received().ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogGroupEventAsync(group, EventType.Group_Updated);
        Assert.True(group.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_NonExistingOrganizationId_ThrowsBadRequest(Group group, SutProvider<GroupService> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(group));
        Assert.Contains("Organization not found", exception.Message);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public async Task SaveAsync_OrganizationDoesNotUseGroups_ThrowsBadRequest(Group group, Organization organization, SutProvider<GroupService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(group));

        Assert.Contains("This organization cannot use groups", exception.Message);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
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
