using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationGroups;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using SendGrid.Helpers.Errors.Model;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationGroups;

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
    public async Task DeleteManyAsync_DeletesManyGroup(Organization org, Group group, Group group2, SutProvider<DeleteGroupCommand> sutProvider)
    {
        // Arrange
        var groupIds = new[] { group.Id, group2.Id };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(groupIds)
            .Returns(new List<Group> { group, group2 });

        // Act
        var result = await sutProvider.Sut.DeleteManyAsync(org.Id, groupIds);

        // Assert
        await sutProvider.GetDependency<IGroupRepository>().Received()
            .DeleteManyAsync(org.Id, Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(groupIds)));

        Assert.Contains(group, result);
        Assert.Contains(group2, result);

        await sutProvider.GetDependency<IEventService>().Received().LogGroupEventAsync(group, EventType.Group_Deleted, Arg.Any<DateTime>());
        await sutProvider.GetDependency<IEventService>().Received().LogGroupEventAsync(group2, EventType.Group_Deleted, Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    [OrganizationCustomize]
    public async Task DeleteManyAsync_WrongOrg_Fails(Organization org, Group group, Group group2, SutProvider<DeleteGroupCommand> sutProvider)
    {
        // Arrange
        var groupIds = new[] { group.Id, group2.Id };
        org.Id = Guid.NewGuid(); // Org no longer associated with groups

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(groupIds)
            .Returns(new List<Group> { group, group2 });

        // Act
        try
        {
            await sutProvider.Sut.DeleteManyAsync(org.Id, groupIds);
        }
        catch (Exception ex)
        {
            // Assert
            Assert.IsType<BadRequestException>(ex);
            Assert.Equal("Groups invalid.", ex.Message);
        }

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceive().DeleteManyAsync(org.Id, Arg.Any<IEnumerable<Guid>>());
    }
}
