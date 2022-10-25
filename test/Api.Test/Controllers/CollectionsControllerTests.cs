using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(CollectionsController))]
[SutProviderCustomize]
public class CollectionsControllerTests
{
    [Theory, BitAutoData]
    public async Task Post_Success(Guid orgId, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .CreateNewCollections(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .EditAnyCollection(orgId)
            .Returns(false);

        var collectionRequest = new CollectionRequestModel
        {
            Name = "encrypted_string",
            ExternalId = "my_external_id"
        };

        _ = await sutProvider.Sut.Post(orgId, collectionRequest);

        await sutProvider.GetDependency<ICollectionService>()
            .Received(1)
            .SaveAsync(Arg.Any<Collection>(), Arg.Any<IEnumerable<CollectionAccessSelection>>(), null);
    }

    [Theory, BitAutoData]
    public async Task Put_Success(Guid orgId, Guid collectionId, Guid userId, CollectionRequestModel collectionRequest,
        SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .ViewAssignedCollections(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .EditAssignedCollections(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(userId);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdAsync(collectionId, userId)
            .Returns(new CollectionDetails
            {
                OrganizationId = orgId,
            });

        _ = await sutProvider.Sut.Put(orgId, collectionId, collectionRequest);
    }

    [Theory, BitAutoData]
    public async Task Put_CanNotEditAssignedCollection_ThrowsNotFound(Guid orgId, Guid collectionId, Guid userId, CollectionRequestModel collectionRequest,
        SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .EditAssignedCollections(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(userId);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdAsync(collectionId, userId)
            .Returns(Task.FromResult<CollectionDetails>(null));

        _ = await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.Put(orgId, collectionId, collectionRequest));
    }

    [Theory, BitAutoData]
    public async Task DeleteMany_Success(Guid orgId, User user, Collection collection1, Collection collection2, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        var model = new CollectionBulkDeleteRequestModel
        {
            Ids = new[] { collection1.Id.ToString(), collection2.Id.ToString() },
            OrganizationId = orgId.ToString()
        };

        var collections = new List<CollectionDetails>
            {
                new CollectionDetails
                {
                    Id = collection1.Id,
                    OrganizationId = orgId,
                },
                new CollectionDetails
                {
                    Id = collection2.Id,
                    OrganizationId = orgId,
                },
            };

        sutProvider.GetDependency<ICurrentContext>()
            .DeleteAssignedCollections(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(user.Id);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(collections);

        // Act
        await sutProvider.Sut.DeleteMany(model);

        // Assert
        await sutProvider.GetDependency<IDeleteCollectionCommand>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Collection>>(coll => coll.Select(c => c.Id).SequenceEqual(collections.Select(c => c.Id))));

    }

    [Theory, BitAutoData]
    public async Task DeleteMany_CanNotDeleteAssignedCollection_ThrowsNotFound(Guid orgId, Collection collection1, Collection collection2, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        var model = new CollectionBulkDeleteRequestModel
        {
            Ids = new[] { collection1.Id.ToString(), collection2.Id.ToString() },
            OrganizationId = orgId.ToString()
        };

        sutProvider.GetDependency<ICurrentContext>()
            .DeleteAssignedCollections(orgId)
            .Returns(false);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteMany(model));

        await sutProvider.GetDependency<IDeleteCollectionCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync((IEnumerable<Collection>)default);

    }


    [Theory, BitAutoData]
    public async Task DeleteMany_UserCanNotAccessCollections_FiltersOutInvalid(Guid orgId, User user, Collection collection1, Collection collection2, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        var model = new CollectionBulkDeleteRequestModel
        {
            Ids = new[] { collection1.Id.ToString(), collection2.Id.ToString() },
            OrganizationId = orgId.ToString()
        };

        var collections = new List<CollectionDetails>
            {
                new CollectionDetails
                {
                    Id = collection2.Id,
                    OrganizationId = orgId,
                },
            };

        sutProvider.GetDependency<ICurrentContext>()
            .DeleteAssignedCollections(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(user.Id);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(collections);

        // Act
        await sutProvider.Sut.DeleteMany(model);

        // Assert
        await sutProvider.GetDependency<IDeleteCollectionCommand>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Collection>>(coll => coll.Select(c => c.Id).SequenceEqual(collections.Select(c => c.Id))));
    }


}
