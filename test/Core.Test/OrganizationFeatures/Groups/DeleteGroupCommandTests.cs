using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class DeleteGroupCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteGroup_Success(SutProvider<DeleteGroupCommand> sutProvider, Group group)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        await sutProvider.Sut.DeleteGroupAsync(group.OrganizationId, group.Id);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).DeleteAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Core.Enums.EventType.Group_Deleted);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteGroup_NotFound_Throws(SutProvider<DeleteGroupCommand> sutProvider, Guid organizationId, Guid groupId)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteGroupAsync(organizationId, groupId));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteGroup_MismatchingOrganizationId_Throws(SutProvider<DeleteGroupCommand> sutProvider, Guid organizationId, Guid groupId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(groupId)
            .Returns(new Core.Entities.Group
            {
                Id = groupId,
                OrganizationId = Guid.NewGuid()
            });

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.DeleteGroupAsync(organizationId, groupId));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteGroup_WithEventSystemUser_Success(SutProvider<DeleteGroupCommand> sutProvider, Group group,
        EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        await sutProvider.Sut.DeleteGroupAsync(group.OrganizationId, group.Id, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).DeleteAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogGroupEventAsync(group, Core.Enums.EventType.Group_Deleted, eventSystemUser);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_DeletesGroup(Group group, SutProvider<DeleteGroupCommand> sutProvider)
    {
        // Act
        await sutProvider.Sut.DeleteAsync(group);

        // Assert
        await sutProvider.GetDependency<IGroupRepository>().Received().DeleteAsync(group);
        await sutProvider.GetDependency<IEventService>().Received().LogGroupEventAsync(group, EventType.Group_Deleted);
    }

    [Theory, BitAutoData]
    [OrganizationCustomize]
    public async Task DeleteManyAsync_DeletesManyGroup(Group group, Group group2, SutProvider<DeleteGroupCommand> sutProvider)
    {
        // Arrange
        var groups = new[] { group, group2 };

        // Act
        await sutProvider.Sut.DeleteManyAsync(groups);

        // Assert
        await sutProvider.GetDependency<IGroupRepository>().Received()
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(groups.Select(g => g.Id))));

        await sutProvider.GetDependency<IEventService>().Received().LogGroupEventsAsync(
            Arg.Is<IEnumerable<(Group, EventType, EventSystemUser?, DateTime?)>>(a =>
                a.All(g => groups.Contains(g.Item1) && g.Item2 == EventType.Group_Deleted))
            );
    }
}
