using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class UpdateGroupCommandTests
{
    private static readonly DateTime _expectedTimestamp = DateTime.UtcNow.AddYears(1);

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_Success(Group group, Group oldGroup,
        Organization organization)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        await sutProvider.Sut.UpdateGroupAsync(group, organization);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        Assert.Equal(_expectedTimestamp, group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithCollections_Success(Group group,
        Group oldGroup, Organization organization, List<CollectionAccessSelection> collections)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        // Arrange list of collections to make sure Manage is mutually exclusive
        for (var i = 0; i < collections.Count; i++)
        {
            var cas = collections[i];
            cas.Manage = i != collections.Count - 1;
            cas.HidePasswords = i == collections.Count - 1;
            cas.ReadOnly = i == collections.Count - 1;
        }

        await sutProvider.Sut.UpdateGroupAsync(group, organization, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        Assert.Equal(_expectedTimestamp, group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithEventSystemUser_Success(Group group,
        Group oldGroup, Organization organization, EventSystemUser eventSystemUser)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        await sutProvider.Sut.UpdateGroupAsync(group, organization, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated, eventSystemUser);
        Assert.Equal(_expectedTimestamp, group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithNullOrganization_Throws(Group group,
        Group oldGroup, EventSystemUser eventSystemUser)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateGroupAsync(group, null, eventSystemUser));

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    [Theory, OrganizationCustomize(UseGroups = false), BitAutoData]
    public async Task UpdateGroup_WithUseGroupsAsFalse_Throws(
        Organization organization, Group group, Group oldGroup, EventSystemUser eventSystemUser)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateGroupAsync(group, organization, eventSystemUser));

        Assert.Contains("This organization cannot use groups", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_GroupBelongsToDifferentOrganization_Throws(
        Group group, Group oldGroup, Organization organization)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        // Mismatching orgId
        oldGroup.OrganizationId = CoreHelpers.GenerateComb();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateGroupAsync(group, organization));
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_CollectionsBelongsToDifferentOrganization_Throws(
        Group group, Group oldGroup, Organization organization, List<CollectionAccessSelection> collectionAccess)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Collection { Id = guid, OrganizationId = CoreHelpers.GenerateComb() }).ToList());

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, collectionAccess));
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_CollectionsDoNotExist_Throws(
        Group group, Group oldGroup, Organization organization, List<CollectionAccessSelection> collectionAccess)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);

        // Return result is missing a collection
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var result = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(guid => new Collection { Id = guid, OrganizationId = group.OrganizationId }).ToList();
                result.RemoveAt(0);
                return result;
            });

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, collectionAccess));
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_WithDefaultUserCollectionType_Throws(
        Group group, Group oldGroup, Organization organization, List<CollectionAccessSelection> collectionAccess)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);

        // Return collections with DefaultUserCollection type
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Collection { Id = guid, OrganizationId = group.OrganizationId, Type = CollectionType.DefaultUserCollection }).ToList());

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, collectionAccess));
        Assert.Contains("You cannot modify group access for collections with the type as DefaultUserCollection.", exception.Message);
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_MemberBelongsToDifferentOrganization_Throws(
        Group group, Group oldGroup, Organization organization, IEnumerable<Guid> userAccess)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeCollections(sutProvider, group);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new OrganizationUser { Id = guid, OrganizationId = CoreHelpers.GenerateComb() }).ToList());

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, null, userAccess));
    }

    [Theory, OrganizationCustomize(UseGroups = true), BitAutoData]
    public async Task UpdateGroup_MemberDoesNotExist_Throws(
        Group group, Group oldGroup, Organization organization, IEnumerable<Guid> userAccess)
    {
        var sutProvider = SetupSutProvider();
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeCollections(sutProvider, group);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var result = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(guid => new OrganizationUser { Id = guid, OrganizationId = group.OrganizationId })
                    .ToList();
                result.RemoveAt(0);
                return result;
            });

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, null, userAccess));
    }

    private static SutProvider<UpdateGroupCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<UpdateGroupCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_expectedTimestamp);
        return sutProvider;
    }

    private void ArrangeGroup(SutProvider<UpdateGroupCommand> sutProvider, Group group, Group oldGroup)
    {
        oldGroup.OrganizationId = group.OrganizationId;
        oldGroup.Id = group.Id;
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(oldGroup);
    }

    private void ArrangeCollections(SutProvider<UpdateGroupCommand> sutProvider, Group group)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Collection() { Id = guid, OrganizationId = group.OrganizationId }).ToList());
    }

    private void ArrangeUsers(SutProvider<UpdateGroupCommand> sutProvider, Group group)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new OrganizationUser { Id = guid, OrganizationId = group.OrganizationId }).ToList());
    }
}
