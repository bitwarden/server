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
    public async Task CreateGroup_WithCollections_Success(SutProvider<CreateGroupCommand> sutProvider, Organization organization, Group group, List<SelectionReadOnly> collections)
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

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public void Validate_WithNullOrganization_ThrowsBadRequest(SutProvider<CreateGroupCommand> sutProvider)
    {
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(null));

        Assert.Contains("Organization not found", exception.Message);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public void Validate_WithUseGroupsAsFalse_ThrowsBadRequest(SutProvider<CreateGroupCommand> sutProvider, Organization organization)
    {
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.Validate(organization));

        Assert.Contains("This organization cannot use groups", exception.Message);
    }
}
