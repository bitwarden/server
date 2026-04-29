using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class CreateGroupCommandTests
{
    private static readonly DateTime _expectedTimestamp = DateTime.UtcNow.AddYears(1);

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_Success(Organization organization, Group group)
    {
        var sutProvider = SetupSutProvider();

        await sutProvider.Sut.CreateGroupAsync(group, organization);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created);
        Assert.Equal(_expectedTimestamp, group.CreationDate);
        Assert.Equal(_expectedTimestamp, group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_WithCollections_Success(Organization organization, Group group, List<CollectionAccessSelection> collections)
    {
        var sutProvider = SetupSutProvider();

        // Arrange list of collections to make sure Manage is mutually exclusive
        for (var i = 0; i < collections.Count; i++)
        {
            var cas = collections[i];
            cas.Manage = i != collections.Count - 1;
            cas.HidePasswords = i == collections.Count - 1;
            cas.ReadOnly = i == collections.Count - 1;
        }

        await sutProvider.Sut.CreateGroupAsync(group, organization, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created);
        Assert.Equal(_expectedTimestamp, group.CreationDate);
        Assert.Equal(_expectedTimestamp, group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_WithEventSystemUser_Success(Organization organization, Group group, EventSystemUser eventSystemUser)
    {
        var sutProvider = SetupSutProvider();

        await sutProvider.Sut.CreateGroupAsync(group, organization, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).CreateAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Created, eventSystemUser);
        Assert.Equal(_expectedTimestamp, group.CreationDate);
        Assert.Equal(_expectedTimestamp, group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task CreateGroup_WithNullOrganization_Throws(Group group, EventSystemUser eventSystemUser)
    {
        var sutProvider = SetupSutProvider();

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateGroupAsync(group, null, eventSystemUser));

        Assert.Contains("Organization not found", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public async Task CreateGroup_WithUseGroupsAsFalse_Throws(Organization organization, Group group, EventSystemUser eventSystemUser)
    {
        var sutProvider = SetupSutProvider();

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.CreateGroupAsync(group, organization, eventSystemUser));

        Assert.Contains("This organization cannot use groups", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    private static SutProvider<CreateGroupCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<CreateGroupCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_expectedTimestamp);
        return sutProvider;
    }
}
