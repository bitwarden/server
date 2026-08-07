using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;

[SutProviderCustomize]
public class ModifyCollectionGroupAccessCommandTests
{
    [Theory, BitAutoData]
    public async Task ModifyAsync_ValidationFails_ReturnsErrorWithoutPersisting(
        SutProvider<ModifyCollectionGroupAccessCommand> sutProvider,
        ModifyCollectionGroupAccessRequest request)
    {
        sutProvider.GetDependency<IModifyCollectionGroupAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Invalid(request, new DuplicateGroupId()));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<DuplicateGroupId>(result.AsError);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .ModifyGroupAccessAsync(default, default, default, default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogCollectionEventsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_AllEmpty_ReturnsSuccessWithoutValidatingOrPersisting(
        SutProvider<ModifyCollectionGroupAccessCommand> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails)
    {
        var request = new ModifyCollectionGroupAccessRequest(
            [new CollectionGroupAccessTarget(collection, accessDetails)], [], [], [], null, false);

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IModifyCollectionGroupAccessValidator>().DidNotReceiveWithAnyArgs()
            .ValidateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .ModifyGroupAccessAsync(default, default, default, default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogCollectionEventsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_ValidRequest_UpsertsAddAndUpdateSelections(
        SutProvider<ModifyCollectionGroupAccessCommand> sutProvider,
        Collection collection,
        Guid addGroupId,
        Guid updateGroupId)
    {
        var accessDetails = AccessDetails(updateGroupId);
        var request = new ModifyCollectionGroupAccessRequest(
            [new CollectionGroupAccessTarget(collection, accessDetails)],
            [new CollectionAccessSelection { Id = addGroupId, Manage = true }],
            [new CollectionAccessSelection { Id = updateGroupId, Manage = false }],
            [],
            null,
            false);

        sutProvider.GetDependency<IModifyCollectionGroupAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyGroupAccessAsync(
            collection.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == collection.Id),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(selections =>
                selections.Any(s => s.Id == addGroupId) && selections.Any(s => s.Id == updateGroupId)),
            Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()),
            Arg.Any<DateTime>());
        await sutProvider.GetDependency<IEventService>().Received(1).LogCollectionEventsAsync(
            Arg.Is<IEnumerable<(Collection, EventType, DateTime?)>>(events =>
                events.Count() == 1 && events.Single().Item1 == collection
                && events.Single().Item2 == EventType.Collection_Updated));
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_ValidRequest_DeletesEachRemovedGroup(
        SutProvider<ModifyCollectionGroupAccessCommand> sutProvider,
        Collection collection,
        Guid removedGroupId1,
        Guid removedGroupId2)
    {
        var accessDetails = AccessDetails(removedGroupId1, removedGroupId2);
        var request = new ModifyCollectionGroupAccessRequest(
            [new CollectionGroupAccessTarget(collection, accessDetails)],
            [], [], [removedGroupId1, removedGroupId2], null, false);

        sutProvider.GetDependency<IModifyCollectionGroupAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyGroupAccessAsync(
            collection.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == collection.Id),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(selections => !selections.Any()),
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(removedGroupId1) && ids.Contains(removedGroupId2)),
            Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_RemoveIdNotACollectionMember_FiltersItOutBeforePersisting(
        SutProvider<ModifyCollectionGroupAccessCommand> sutProvider,
        Collection collection,
        Guid actualMemberId,
        Guid notAMemberId)
    {
        // notAMemberId is a valid id but was never granted access to this collection. It has to be dropped,
        // not forwarded to the repository, or it would bump an unrelated group's revision date for no reason.
        var accessDetails = AccessDetails(actualMemberId);
        var request = new ModifyCollectionGroupAccessRequest(
            [new CollectionGroupAccessTarget(collection, accessDetails)],
            [], [], [actualMemberId, notAMemberId], null, false);

        sutProvider.GetDependency<IModifyCollectionGroupAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyGroupAccessAsync(
            collection.OrganizationId,
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(actualMemberId) && !ids.Contains(notAMemberId)),
            Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_ValidRequest_UpsertsAndRemovesInOneAtomicCall(
        SutProvider<ModifyCollectionGroupAccessCommand> sutProvider,
        Collection collection,
        Guid addGroupId,
        Guid removedGroupId)
    {
        var accessDetails = AccessDetails(removedGroupId);
        var request = new ModifyCollectionGroupAccessRequest(
            [new CollectionGroupAccessTarget(collection, accessDetails)],
            [new CollectionAccessSelection { Id = addGroupId, Manage = true }], [], [removedGroupId], null, false);

        sutProvider.GetDependency<IModifyCollectionGroupAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        // Regression test: upserts and removes must go through one repository call, not two independently-failable ones.
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyGroupAccessAsync(
            collection.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == collection.Id),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(selections => selections.Any(s => s.Id == addGroupId)),
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(removedGroupId)),
            Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_MultipleTargets_AppliesSameDeltaToAllInOneCall(
        SutProvider<ModifyCollectionGroupAccessCommand> sutProvider,
        Collection collectionA,
        Collection collectionB,
        Guid addGroupId)
    {
        var targets = new[]
        {
            new CollectionGroupAccessTarget(collectionA, AccessDetails()),
            new CollectionGroupAccessTarget(collectionB, AccessDetails())
        };
        var request = new ModifyCollectionGroupAccessRequest(
            targets, [new CollectionAccessSelection { Id = addGroupId, Manage = true }], [], [], null, false);

        sutProvider.GetDependency<IModifyCollectionGroupAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyGroupAccessAsync(
            collectionA.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionA.Id) && ids.Contains(collectionB.Id)),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(selections => selections.Any(s => s.Id == addGroupId)),
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<DateTime>());
        await sutProvider.GetDependency<IEventService>().Received(1).LogCollectionEventsAsync(
            Arg.Is<IEnumerable<(Collection, EventType, DateTime?)>>(events => events.Count() == 2));
    }

    private static CollectionAccessDetails AccessDetails(params Guid[] existingMemberIds) => new()
    {
        Users = [],
        Groups = existingMemberIds.Select(id => new CollectionAccessSelection { Id = id }).ToList()
    };
}
