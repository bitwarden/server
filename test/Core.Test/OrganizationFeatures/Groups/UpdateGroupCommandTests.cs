using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
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
public class UpdateGroupCommandTests
{
    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_Success(SutProvider<UpdateGroupCommand> sutProvider, Organization organization, Group group)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(group.OrganizationId).Returns(organization);

        await sutProvider.Sut.UpdateGroupAsync(group);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        Assert.True(group.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithCollections_Success(SutProvider<UpdateGroupCommand> sutProvider, Organization organization, Group group, List<SelectionReadOnly> collections)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(group.OrganizationId).Returns(organization);

        await sutProvider.Sut.UpdateGroupAsync(group, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        Assert.True(group.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithEventSystemUser_Success(SutProvider<UpdateGroupCommand> sutProvider, Organization organization, Group group, EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(group.OrganizationId).Returns(organization);

        await sutProvider.Sut.UpdateGroupAsync(group, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated, eventSystemUser);
        Assert.True(group.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task UpdateGroup_NonExistingOrganizationId_ThrowsBadRequest(SutProvider<UpdateGroupCommand> sutProvider, Group group)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateGroupAsync(group));
        Assert.Contains("Organization not found", exception.Message);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public async Task UpdateGroup_OrganizationDoesNotUseGroups_ThrowsBadRequest(SutProvider<UpdateGroupCommand> sutProvider, Organization organization, Group group)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateGroupAsync(group));

        Assert.Contains("This organization cannot use groups", exception.Message);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }
}
