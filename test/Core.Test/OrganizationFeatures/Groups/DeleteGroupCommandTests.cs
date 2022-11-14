using Bit.Core.Entities;
using Bit.Core.Enums;
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
            Arg.Is<IEnumerable<(Group, EventType, DateTime?)>>(a =>
                a.All(g => groups.Contains(g.Item1) && g.Item2 == EventType.Group_Deleted))
            );
    }
}
