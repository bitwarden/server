using Bit.Api.Models.Public.Response;
using Bit.Api.Public.Controllers;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Public.Controllers;

[ControllerCustomize(typeof(CollectionsController))]
[SutProviderCustomize]
public class CollectionsControllerTests
{
    [Theory, BitAutoData]
    public async Task Get_WithDefaultUserCollection_ReturnsNotFound(
        Collection collection, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        collection.Type = CollectionType.DefaultUserCollection;
        var access = new CollectionAccessDetails
        {
            Groups = new List<CollectionAccessSelection>(),
            Users = new List<CollectionAccessSelection>()
        };

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationId.Returns(collection.OrganizationId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, access));

        // Act
        var result = await sutProvider.Sut.Get(collection.Id);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task Get_WithSharedCollection_ReturnsCollection(
        Collection collection, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        collection.Type = CollectionType.SharedCollection;
        var access = new CollectionAccessDetails
        {
            Groups = [],
            Users = []
        };

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationId.Returns(collection.OrganizationId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, access));

        // Act
        var result = await sutProvider.Sut.Get(collection.Id);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<CollectionResponseModel>(jsonResult.Value);
        Assert.Equal(collection.Id, response.Id);
        Assert.Equal(collection.Type, response.Type);
    }

    [Theory, BitAutoData]
    public async Task Delete_WithDefaultUserCollection_ReturnsBadRequest(
        Collection collection, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        collection.Type = CollectionType.DefaultUserCollection;

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationId.Returns(collection.OrganizationId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdAsync(collection.Id)
            .Returns(collection);

        // Act
        var result = await sutProvider.Sut.Delete(collection.Id);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponseModel>(badRequestResult.Value);
        Assert.Contains("You cannot delete a collection with the type as DefaultUserCollection", errorResponse.Message);

        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .DeleteAsync(Arg.Any<Collection>());
    }

    [Theory, BitAutoData]
    public async Task Delete_WithSharedCollection_ReturnsOk(
        Collection collection, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        collection.Type = CollectionType.SharedCollection;

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationId.Returns(collection.OrganizationId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdAsync(collection.Id)
            .Returns(collection);

        // Act
        var result = await sutProvider.Sut.Delete(collection.Id);

        // Assert
        Assert.IsType<OkResult>(result);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .DeleteAsync(collection);
    }
}
