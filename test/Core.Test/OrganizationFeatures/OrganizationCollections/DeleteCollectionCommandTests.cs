
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
    public async Task DeleteManyAsync_DeletesManyCollections(Organization org, Collection collection, Collection collection2, SutProvider<DeleteCollectionCommand> sutProvider)
    {
        // Arrange
        var collectionIds = new[] { collection.Id, collection2.Id };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIds(collectionIds)
            .Returns(new List<Collection> { collection, collection2 });

        // Act
        var result = await sutProvider.Sut.DeleteManyAsync(org.Id, collectionIds);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>().Received()
            .DeleteManyAsync(org.Id, Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(collectionIds)));

        Assert.Contains(collection, result);
        Assert.Contains(collection2, result);

        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventAsync(collection, EventType.Collection_Deleted, Arg.Any<DateTime>());
        await sutProvider.GetDependency<IEventService>().Received().LogCollectionEventAsync(collection2, EventType.Collection_Deleted, Arg.Any<DateTime>());
    }


    [Theory, BitAutoData]
    [OrganizationCustomize]
    public async Task DeleteManyAsync_WrongOrg_Fails(Organization org, Collection collection, Collection collection2, SutProvider<DeleteCollectionCommand> sutProvider)
    {
        // Arrange
        var collectionIds = new[] { collection.Id, collection2.Id };
        org.Id = Guid.NewGuid(); // Org no longer associated with collections

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIds(collectionIds)
            .Returns(new List<Collection> { collection, collection2 });

        // Act
        try
        {
            await sutProvider.Sut.DeleteManyAsync(org.Id, collectionIds);
        }
        catch (Exception ex)
        {
            // Assert
            Assert.IsType<BadRequestException>(ex);
            Assert.Equal("Collections not associated with provided organization", ex.Message);
        }

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().DeleteManyAsync(org.Id, Arg.Any<IEnumerable<Guid>>());
    }
}
