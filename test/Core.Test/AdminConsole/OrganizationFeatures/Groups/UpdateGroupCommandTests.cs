﻿using Bit.Core.AdminConsole.Entities;
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
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class UpdateGroupCommandTests
{
    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = false), BitAutoData]
    public async Task UpdateGroup_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group, Group oldGroup,
        Organization organization)
    {
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        await sutProvider.Sut.UpdateGroupAsync(group, organization);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = false), BitAutoData]
    public async Task UpdateGroup_WithCollections_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group,
        Group oldGroup, Organization organization, List<CollectionAccessSelection> collections)
    {
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        await sutProvider.Sut.UpdateGroupAsync(group, organization, collections);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group, collections);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = false), BitAutoData]
    public async Task UpdateGroup_WithEventSystemUser_Success(SutProvider<UpdateGroupCommand> sutProvider, Group group,
        Group oldGroup, Organization organization, EventSystemUser eventSystemUser)
    {
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        await sutProvider.Sut.UpdateGroupAsync(group, organization, eventSystemUser);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).ReplaceAsync(group);
        await sutProvider.GetDependency<IEventService>().Received(1).LogGroupEventAsync(group, Enums.EventType.Group_Updated, eventSystemUser);
        AssertHelper.AssertRecent(group.RevisionDate);
    }

    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = false), BitAutoData]
    public async Task UpdateGroup_WithNullOrganization_Throws(SutProvider<UpdateGroupCommand> sutProvider, Group group,
        Group oldGroup, EventSystemUser eventSystemUser)
    {
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateGroupAsync(group, null, eventSystemUser));

        Assert.Contains("Organization not found", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    [Theory, OrganizationCustomize(UseGroups = false, FlexibleCollections = false), BitAutoData]
    public async Task UpdateGroup_WithUseGroupsAsFalse_Throws(SutProvider<UpdateGroupCommand> sutProvider,
        Organization organization, Group group, Group oldGroup, EventSystemUser eventSystemUser)
    {
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateGroupAsync(group, organization, eventSystemUser));

        Assert.Contains("This organization cannot use groups", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }

    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = true), BitAutoData]
    public async Task UpdateGroup_WithFlexibleCollections_WithAccessAll_Throws(
        SutProvider<UpdateGroupCommand> sutProvider, Organization organization, Group group, Group oldGroup)
    {
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        group.AccessAll = true;
        organization.FlexibleCollections = true;

        var exception =
            await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.UpdateGroupAsync(group, organization));
        Assert.Contains("AccessAll property has been deprecated", exception.Message);

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogGroupEventAsync(default, default, default);
    }


    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = true), BitAutoData]
    public async Task UpdateGroup_GroupBelongsToDifferentOrganization_Throws(SutProvider<UpdateGroupCommand> sutProvider,
        Group group, Group oldGroup, Organization organization)
    {
        group.AccessAll = false;
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);
        ArrangeCollections(sutProvider, group);

        // Mismatching orgId
        oldGroup.OrganizationId = CoreHelpers.GenerateComb();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateGroupAsync(group, organization));
        Assert.Contains("cannot change a group's organization id", exception.Message);
    }

    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = true), BitAutoData]
    public async Task UpdateGroup_CollectionsBelongsToDifferentOrganization_Throws(SutProvider<UpdateGroupCommand> sutProvider,
        Group group, Group oldGroup, Organization organization, List<CollectionAccessSelection> collectionAccess)
    {
        group.AccessAll = false;
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeUsers(sutProvider, group);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Collection { Id = guid, OrganizationId = CoreHelpers.GenerateComb() }).ToList());

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, collectionAccess));
        Assert.Contains("A collection does not exist or you do not have permission", exception.Message);
    }

    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = true), BitAutoData]
    public async Task UpdateGroup_CollectionsDoNotExist_Throws(SutProvider<UpdateGroupCommand> sutProvider,
        Group group, Group oldGroup, Organization organization, List<CollectionAccessSelection> collectionAccess)
    {
        group.AccessAll = false;
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

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, collectionAccess));
        Assert.Contains("A collection does not exist or you do not have permission", exception.Message);
    }

    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = true), BitAutoData]
    public async Task UpdateGroup_MemberBelongsToDifferentOrganization_Throws(SutProvider<UpdateGroupCommand> sutProvider,
        Group group, Group oldGroup, Organization organization, IEnumerable<Guid> userAccess)
    {
        group.AccessAll = false;
        ArrangeGroup(sutProvider, group, oldGroup);
        ArrangeCollections(sutProvider, group);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new OrganizationUser { Id = guid, OrganizationId = CoreHelpers.GenerateComb() }).ToList());

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, null, userAccess));
        Assert.Contains("A member does not exist or you do not have permission", exception.Message);
    }

    [Theory, OrganizationCustomize(UseGroups = true, FlexibleCollections = true), BitAutoData]
    public async Task UpdateGroup_MemberDoesNotExist_Throws(SutProvider<UpdateGroupCommand> sutProvider,
        Group group, Group oldGroup, Organization organization, IEnumerable<Guid> userAccess)
    {
        group.AccessAll = false;
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

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateGroupAsync(group, organization, null, userAccess));
        Assert.Contains("A member does not exist or you do not have permission", exception.Message);
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
