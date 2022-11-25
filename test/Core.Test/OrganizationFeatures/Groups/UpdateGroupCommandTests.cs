using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.Groups;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class UpdateGroupCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateGroup_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        await sutProvider.Sut.UpdateGroupAsync(group);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        Assert.True(group.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateGroup_WithCollections_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group, List<SelectionReadOnly> collections)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        await sutProvider.Sut.UpdateGroupAsync(group, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        Assert.True(group.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateGroup_WithEventSystemUser_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group, EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        await sutProvider.Sut.UpdateGroupAsync(group, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated, eventSystemUser);
        Assert.True(group.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }
}
