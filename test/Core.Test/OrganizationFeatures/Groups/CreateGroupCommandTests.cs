using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
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
public class CreateGroupCommandTests
{
    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_Success(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(group.OrganizationId).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateGroupAsync(group);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created);
        Assert.True(group.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(group.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(r => r.Type == ReferenceEventType.GroupCreated && r.Id == organization.Id && r.Source == ReferenceEventSource.Organization));
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_WithCollections_Success(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group, List<SelectionReadOnly> collections)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(group.OrganizationId).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateGroupAsync(group, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created);
        Assert.True(group.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(group.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(r => r.Type == ReferenceEventType.GroupCreated && r.Id == organization.Id && r.Source == ReferenceEventSource.Organization));
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_WithEventSystemUser_Success(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group, EventSystemUser eventSystemUser)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(group.OrganizationId).Returns(organization);
        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.CreateGroupAsync(group, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created, eventSystemUser);
        Assert.True(group.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(group.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(r => r.Type == ReferenceEventType.GroupCreated && r.Id == organization.Id && r.Source == ReferenceEventSource.Organization));
    }

    [Theory, BitAutoData]
    public async Task CreateGroup_NonExistingOrganizationId_ThrowsBadRequest(SutProvider<CreateGroupCommand> sutProvider, Group group)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateGroupAsync(group));
        Assert.Contains("Organization not found", exception.Message);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
        await sutProvider.GetDependency<IReferenceEventService>().DidNotReceiveWithAnyArgs().RaiseEventAsync(default);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public async Task CreateGroup_OrganizationDoesNotUseGroups_ThrowsBadRequest(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateGroupAsync(group));

        Assert.Contains("This organization cannot use groups", exception.Message);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
        await sutProvider.GetDependency<IReferenceEventService>().DidNotReceiveWithAnyArgs().RaiseEventAsync(default);
    }
}
