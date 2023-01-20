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
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class CreateGroupCommandTests
{
    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_Success(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group)
    {
        await sutProvider.Sut.CreateGroupAsync(group, organization);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created);
        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(r => r.Type == ReferenceEventType.GroupCreated && r.Id == organization.Id && r.Source == ReferenceEventSource.Organization));
        AssertHelper.AssertRecent(group.CreationDate);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_WithCollections_Success(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group, List<CollectionAccessSelection> collections)
    {
        await sutProvider.Sut.CreateGroupAsync(group, organization, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created);
        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(r => r.Type == ReferenceEventType.GroupCreated && r.Id == organization.Id && r.Source == ReferenceEventSource.Organization));
        AssertHelper.AssertRecent(group.CreationDate);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_WithEventSystemUser_Success(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group, EventSystemUser eventSystemUser)
    {
        await sutProvider.Sut.CreateGroupAsync(group, organization, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created, eventSystemUser);
        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(r => r.Type == ReferenceEventType.GroupCreated && r.Id == organization.Id && r.Source == ReferenceEventSource.Organization));
        AssertHelper.AssertRecent(group.CreationDate);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_WithNullOrganization_Throws(SutProvider<CreateGroupCommand> sutProvider, Group group, EventSystemUser eventSystemUser)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateGroupAsync(group, null, eventSystemUser));

        Assert.Contains("Organization not found", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
        await sutProvider.GetDependency<IReferenceEventService>().DidNotReceiveWithAnyArgs().RaiseEventAsync(default);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public async Task CreateGroup_WithUseGroupsAsFalse_Throws(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group, EventSystemUser eventSystemUser)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateGroupAsync(group, organization, eventSystemUser));

        Assert.Contains("This organization cannot use groups", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
        await sutProvider.GetDependency<IReferenceEventService>().DidNotReceiveWithAnyArgs().RaiseEventAsync(default);
    }
}
