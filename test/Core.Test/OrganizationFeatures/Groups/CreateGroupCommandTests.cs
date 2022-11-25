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
public class CreateGroupCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateGroup_Success(SutProvider<CreateGroupCommand> sutProvider, Group group, Organization organization)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateGroupAsync(group, organization);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created);
        Assert.True(group.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(group.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateGroup_WithCollections_Success(SutProvider<CreateGroupCommand> sutProvider, Group group, Organization organization, List<SelectionReadOnly> collections)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateGroupAsync(group, organization, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created);
        Assert.True(group.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(group.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateGroup_WithEventSystemUser_Success(SutProvider<CreateGroupCommand> sutProvider, Group group, Organization organization, EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateGroupAsync(group, organization, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created, eventSystemUser);
        Assert.True(group.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(group.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }
}
