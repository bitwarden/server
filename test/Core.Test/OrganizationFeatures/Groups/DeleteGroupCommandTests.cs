using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    public async Task DeleteGroup_WithEventSystemUser_Success(SutProvider<DeleteGroupCommand> sutProvider, Group group, EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdAsync(group.Id)
            .Returns(group);

        await sutProvider.Sut.DeleteGroupAsync(group.OrganizationId, group.Id, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).DeleteAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Core.Enums.EventType.Group_Deleted, eventSystemUser);
    }
}
