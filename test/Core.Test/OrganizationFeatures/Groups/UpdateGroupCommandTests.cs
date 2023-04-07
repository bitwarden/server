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
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class UpdateGroupCommandTests
{
    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group, Organization organization)
    {
        await sutProvider.Sut.UpdateGroupAsync(group, organization);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithCollections_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group, Organization organization, List<CollectionAccessSelection> collections)
    {
        await sutProvider.Sut.UpdateGroupAsync(group, organization, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithEventSystemUser_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group, Organization organization, EventSystemUser eventSystemUser)
    {
        await sutProvider.Sut.UpdateGroupAsync(group, organization, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated, eventSystemUser);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithNullOrganization_Throws(SutProvider<UpdateGroupCommand> sutProvider, Group group, EventSystemUser eventSystemUser)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateGroupAsync(group, null, eventSystemUser));

        Assert.Contains("Organization not found", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public async Task UpdateGroup_WithUseGroupsAsFalse_Throws(SutProvider<UpdateGroupCommand> sutProvider, Organization organization, Group group, EventSystemUser eventSystemUser)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateGroupAsync(group, organization, eventSystemUser));

        Assert.Contains("This organization cannot use groups", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }
}
