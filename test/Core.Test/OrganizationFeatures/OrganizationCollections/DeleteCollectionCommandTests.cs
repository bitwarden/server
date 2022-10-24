
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
        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventAsync(collection, EventType.Collection_Deleted);
    }

    [Theory, BitAutoData]
    [OrganizationCustomize]
    public async Task DeleteManyAsync_DeletesManyCollections(Collection collection, Collection collection2, SutProvider<DeleteCollectionCommand> sutProvider)
    {
        // Arrange
        var collectionIds = new[] { collection.Id, collection2.Id };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(collectionIds)
            .Returns(new List<Collection> { collection, collection2 });

        // Act
        await sutProvider.Sut.DeleteManyAsync(collectionIds);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>().Received()
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(collectionIds)));

        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventAsync(collection, EventType.Collection_Deleted, Arg.Any<DateTime>());
        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventAsync(collection2, EventType.Collection_Deleted, Arg.Any<DateTime>());
    }


}
