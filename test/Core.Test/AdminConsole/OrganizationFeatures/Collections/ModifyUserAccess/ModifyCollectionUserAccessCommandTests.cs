using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;
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

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

[SutProviderCustomize]
public class ModifyCollectionUserAccessCommandTests
{
    [Theory, BitAutoData]
    public async Task ModifyAsync_ValidationFails_ReturnsErrorWithoutPersisting(
        SutProvider<ModifyCollectionUserAccessCommand> sutProvider,
        ModifyCollectionUserAccessRequest request)
    {
        sutProvider.GetDependency<IModifyCollectionUserAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Invalid(request, new DuplicateOrganizationUserId()));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<DuplicateOrganizationUserId>(result.AsError);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .ModifyUserAccessAsync(default, default, default, default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogCollectionEventsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_AllEmpty_ReturnsSuccessWithoutValidatingOrPersisting(
        SutProvider<ModifyCollectionUserAccessCommand> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails)
    {
        var request = new ModifyCollectionUserAccessRequest(
            [new CollectionUserAccessTarget(collection, accessDetails)], [], [], [], null, false);

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IModifyCollectionUserAccessValidator>().DidNotReceiveWithAnyArgs()
            .ValidateAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .ModifyUserAccessAsync(default, default, default, default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogCollectionEventsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_ValidRequest_UpsertsAddAndUpdateSelections(
        SutProvider<ModifyCollectionUserAccessCommand> sutProvider,
        Collection collection,
        Guid addUserId,
        Guid updateUserId)
    {
        var accessDetails = AccessDetails(updateUserId);
        var request = new ModifyCollectionUserAccessRequest(
            [new CollectionUserAccessTarget(collection, accessDetails)],
            [new CollectionAccessSelection { Id = addUserId, Manage = true }],
            [new CollectionAccessSelection { Id = updateUserId, Manage = false }],
            [],
            null,
            false);

        sutProvider.GetDependency<IModifyCollectionUserAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyUserAccessAsync(
            collection.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == collection.Id),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(selections =>
                selections.Any(s => s.Id == addUserId) && selections.Any(s => s.Id == updateUserId)),
            Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()),
            Arg.Any<DateTime>());
        await sutProvider.GetDependency<IEventService>().Received(1).LogCollectionEventsAsync(
            Arg.Is<IEnumerable<(Collection, EventType, DateTime?)>>(events =>
                events.Count() == 1 && events.Single().Item1 == collection
                && events.Single().Item2 == EventType.Collection_Updated));
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_ValidRequest_DeletesEachRemovedUser(
        SutProvider<ModifyCollectionUserAccessCommand> sutProvider,
        Collection collection,
        Guid removedUserId1,
        Guid removedUserId2)
    {
        var accessDetails = AccessDetails(removedUserId1, removedUserId2);
        var request = new ModifyCollectionUserAccessRequest(
            [new CollectionUserAccessTarget(collection, accessDetails)],
            [], [], [removedUserId1, removedUserId2], null, false);

        sutProvider.GetDependency<IModifyCollectionUserAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyUserAccessAsync(
            collection.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == collection.Id),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(selections => !selections.Any()),
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(removedUserId1) && ids.Contains(removedUserId2)),
            Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_RemoveIdNotACollectionMember_FiltersItOutBeforePersisting(
        SutProvider<ModifyCollectionUserAccessCommand> sutProvider,
        Collection collection,
        Guid actualMemberId,
        Guid notAMemberId)
    {
        // notAMemberId is a valid id but was never granted access to this collection. It has to be dropped,
        // not forwarded to the repository, or it would bump an unrelated user's AccountRevisionDate for no reason.
        var accessDetails = AccessDetails(actualMemberId);
        var request = new ModifyCollectionUserAccessRequest(
            [new CollectionUserAccessTarget(collection, accessDetails)],
            [], [], [actualMemberId, notAMemberId], null, false);

        sutProvider.GetDependency<IModifyCollectionUserAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyUserAccessAsync(
            collection.OrganizationId,
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(actualMemberId) && !ids.Contains(notAMemberId)),
            Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_ValidRequest_UpsertsAndRemovesInOneAtomicCall(
        SutProvider<ModifyCollectionUserAccessCommand> sutProvider,
        Collection collection,
        Guid addUserId,
        Guid removedUserId)
    {
        var accessDetails = AccessDetails(removedUserId);
        var request = new ModifyCollectionUserAccessRequest(
            [new CollectionUserAccessTarget(collection, accessDetails)],
            [new CollectionAccessSelection { Id = addUserId, Manage = true }], [], [removedUserId], null, false);

        sutProvider.GetDependency<IModifyCollectionUserAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        // Regression test: upserts and removes must go through one repository call, not two independently-failable ones.
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyUserAccessAsync(
            collection.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == collection.Id),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(selections => selections.Any(s => s.Id == addUserId)),
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(removedUserId)),
            Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task ModifyAsync_MultipleTargets_AppliesSameDeltaToAllInOneCall(
        SutProvider<ModifyCollectionUserAccessCommand> sutProvider,
        Collection collectionA,
        Collection collectionB,
        Guid addUserId)
    {
        var targets = new[]
        {
            new CollectionUserAccessTarget(collectionA, AccessDetails()),
            new CollectionUserAccessTarget(collectionB, AccessDetails())
        };
        var request = new ModifyCollectionUserAccessRequest(
            targets, [new CollectionAccessSelection { Id = addUserId, Manage = true }], [], [], null, false);

        sutProvider.GetDependency<IModifyCollectionUserAccessValidator>().ValidateAsync(request)
            .Returns(ValidationResultHelpers.Valid(request));

        var result = await sutProvider.Sut.ModifyAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).ModifyUserAccessAsync(
            collectionA.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionA.Id) && ids.Contains(collectionB.Id)),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(selections => selections.Any(s => s.Id == addUserId)),
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<DateTime>());
        await sutProvider.GetDependency<IEventService>().Received(1).LogCollectionEventsAsync(
            Arg.Is<IEnumerable<(Collection, EventType, DateTime?)>>(events => events.Count() == 2));
    }

    private static CollectionAccessDetails AccessDetails(params Guid[] existingMemberIds) => new()
    {
        Users = existingMemberIds.Select(id => new CollectionAccessSelection { Id = id }).ToList(),
        Groups = []
    };
}
