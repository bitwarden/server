using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationCollections;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationConnections;

[SutProviderCustomize]
public class DeleteCollectionCommandTests
{

    [Theory, BitAutoData]
    [OrganizationCustomize]
    public async Task DeleteAsync_DeletesCollection(Collection collection, SutProvider<DeleteCollectionCommand> sutProvider)
    {
        // Act
        await sutProvider.Sut.DeleteAsync(collection);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>().Received().DeleteAsync(collection);
        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventAsync(collection, EventType.Collection_Deleted, Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    [OrganizationCustomize]
    public async Task DeleteManyAsync_DeletesManyCollections(Collection collection, Collection collection2, SutProvider<DeleteCollectionCommand> sutProvider)
    {
        // Arrange
        var collectionIds = new[] { collection.Id, collection2.Id };
        collection.Type = collection2.Type = CollectionType.SharedCollection;

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(collectionIds)
            .Returns(new List<Collection> { collection, collection2 });

        // Act
        await sutProvider.Sut.DeleteManyAsync(collectionIds);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>().Received()
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(collectionIds)));

        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventsAsync(
            Arg.Is<IEnumerable<(Collection, EventType, DateTime?)>>(a =>
            a.All(c => collectionIds.Contains(c.Item1.Id) && c.Item2 == EventType.Collection_Deleted)));
    }

    [Theory, BitAutoData]
    [OrganizationCustomize]
    public async Task DeleteAsync_WithDefaultUserCollectionType_ThrowsBadRequest(Collection collection, SutProvider<DeleteCollectionCommand> sutProvider)
    {
        // Arrange
        collection.Type = CollectionType.DefaultUserCollection;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteAsync(collection));
        Assert.Contains("You cannot delete a collection with the type as DefaultUserCollection.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    [OrganizationCustomize]
    public async Task DeleteManyAsync_WithDefaultUserCollectionType_ThrowsBadRequest(Collection collection, Collection collection2, SutProvider<DeleteCollectionCommand> sutProvider)
    {
        // Arrange
        collection.Type = CollectionType.DefaultUserCollection;
        collection2.Type = CollectionType.SharedCollection;
        var collections = new List<Collection> { collection, collection2 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteManyAsync(collections));
        Assert.Contains("You cannot delete collections with the type as DefaultUserCollection.", ex.Message);
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogCollectionEventsAsync(default);
    }

}
