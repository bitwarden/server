using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Collections;

[SutProviderCustomize]
public class BulkAddCollectionAccessCommandTests
{
    private static readonly DateTime _expectedRevisionDate = DateTime.UtcNow.AddYears(1);

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AddAccessAsync_Success(
        Organization org,
        ICollection<Collection> collections,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        var sutProvider = SetupSutProvider();
        SetCollectionsToSharedType(collections);

        var userAccessSelections = ToAccessSelection(collectionUsers);
        var groupAccessSelections = ToAccessSelection(collectionGroups);
        await sutProvider.Sut.AddAccessAsync(collections,
            userAccessSelections,
            groupAccessSelections
        );

        await sutProvider.GetDependency<ICollectionRepository>().Received().CreateOrUpdateAccessForManyAsync(
            org.Id,
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(collections.Select(c => c.Id))),
            userAccessSelections,
            groupAccessSelections,
            _expectedRevisionDate);

        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventsAsync(
            Arg.Is<IEnumerable<(Collection, EventType, DateTime?)>>(
                events => events.All(e =>
                    collections.Contains(e.Item1) &&
                    e.Item2 == EventType.Collection_Updated &&
                    e.Item3.HasValue
                )
            )
        );
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_NoCollectionsProvided_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider)
    {
        var exception =
            await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.AddAccessAsync(null, null, null));

        Assert.Contains("No collections were provided.", exception.Message);

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByManyIdsAsync(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_NoCollection_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(Enumerable.Empty<Collection>().ToList(),
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("No collections were provided.", exception.Message);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_DifferentOrgs_Failure(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        ICollection<Collection> collections,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        SetCollectionsToSharedType(collections);

        collections.First().OrganizationId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(collections,
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("All collections must belong to the same organization.", exception.Message);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AddAccessAsync_WithDefaultUserCollectionType_ThrowsBadRequest(SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        IList<Collection> collections,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        // Arrange
        collections.First().Type = CollectionType.DefaultUserCollection;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(collections,
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        Assert.Contains("You cannot add access to collections with the type as DefaultUserCollection.", exception.Message);

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateOrUpdateAccessForManyAsync(default, default, default, default, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCollectionEventsAsync(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task ValidateRequestAsync_WithInvalidAccess_ThrowsBadRequest(
        SutProvider<BulkAddCollectionAccessCommand> sutProvider,
        IList<Collection> collections,
        IEnumerable<CollectionUser> collectionUsers,
        IEnumerable<CollectionGroup> collectionGroups)
    {
        SetCollectionsToSharedType(collections);
        sutProvider.GetDependency<ICollectionAccessValidator>()
            .ValidateAsync(Arg.Any<CollectionAccessValidationRequest>())
            .Returns(callInfo => Invalid(
                callInfo.Arg<CollectionAccessValidationRequest>(), new CollectionAccessInvalidError()));

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.AddAccessAsync(collections,
            ToAccessSelection(collectionUsers),
            ToAccessSelection(collectionGroups)
        ));

        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateOrUpdateAccessForManyAsync(default, default, default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventsAsync(default);
    }

    private static void SetCollectionsToSharedType(IEnumerable<Collection> collections)
    {
        foreach (var collection in collections)
        {
            collection.Type = CollectionType.SharedCollection;
        }
    }

    private static ICollection<CollectionAccessSelection> ToAccessSelection(IEnumerable<CollectionUser> collectionUsers)
    {
        return collectionUsers.Select(cu => new CollectionAccessSelection
        {
            Id = cu.OrganizationUserId,
            Manage = cu.Manage,
            HidePasswords = cu.HidePasswords,
            ReadOnly = cu.ReadOnly
        }).ToList();
    }
    private static ICollection<CollectionAccessSelection> ToAccessSelection(IEnumerable<CollectionGroup> collectionGroups)
    {
        return collectionGroups.Select(cg => new CollectionAccessSelection
        {
            Id = cg.GroupId,
            Manage = cg.Manage,
            HidePasswords = cg.HidePasswords,
            ReadOnly = cg.ReadOnly
        }).ToList();
    }

    private static SutProvider<BulkAddCollectionAccessCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<BulkAddCollectionAccessCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_expectedRevisionDate);
        sutProvider.GetDependency<ICollectionAccessValidator>()
            .ValidateAsync(Arg.Any<CollectionAccessValidationRequest>())
            .Returns(callInfo => Valid(callInfo.Arg<CollectionAccessValidationRequest>()));
        return sutProvider;
    }
}
