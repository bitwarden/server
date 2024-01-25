using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Collection = Bit.Core.Entities.Collection;
using User = Bit.Core.Entities.User;

namespace Bit.Api.Test.Controllers;

/// <summary>
/// CollectionsController tests that use pre-Flexible Collections logic. To be removed when the feature flag is removed.
/// Note the feature flag defaults to OFF so it is not explicitly set in these tests.
/// </summary>
[ControllerCustomize(typeof(CollectionsController))]
[SutProviderCustomize]
public class LegacyCollectionsControllerTests
{
    [Theory, BitAutoData]
    public async Task Post_Manager_AssignsToCollection_Success(Guid orgId, OrganizationUser orgUser, SutProvider<CollectionsController> sutProvider)
    {
        orgUser.Type = OrganizationUserType.Manager;
        orgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationManager(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .EditAnyCollection(orgId)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .EditAssignedCollections(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().UserId = orgUser.UserId;

        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgId, orgUser.UserId.Value)
            .Returns(orgUser);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdAsync(Arg.Any<Guid>(), orgUser.UserId.Value, Arg.Any<bool>())
            .Returns(new CollectionDetails());

        var collectionRequest = new CollectionRequestModel
        {
            Name = "encrypted_string",
            ExternalId = "my_external_id"
        };

        _ = await sutProvider.Sut.Post(orgId, collectionRequest);

        var test = sutProvider.GetDependency<ICollectionService>().ReceivedCalls();
        await sutProvider.GetDependency<ICollectionService>()
            .Received(1)
            .SaveAsync(Arg.Any<Collection>(), Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(users => users.Any(u => u.Id == orgUser.Id && !u.ReadOnly && !u.HidePasswords && !u.Manage)));
    }

    [Theory, BitAutoData]
    public async Task Post_Owner_DoesNotAssignToCollection_Success(Guid orgId, OrganizationUser orgUser, SutProvider<CollectionsController> sutProvider)
    {
        orgUser.Type = OrganizationUserType.Owner;
        orgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationManager(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .EditAnyCollection(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .EditAssignedCollections(orgId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().UserId = orgUser.UserId;

        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgId, orgUser.UserId.Value)
            .Returns(orgUser);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdAsync(Arg.Any<Guid>(), orgUser.UserId.Value, Arg.Any<bool>())
            .Returns(new CollectionDetails());

        var collectionRequest = new CollectionRequestModel
        {
            Name = "encrypted_string",
            ExternalId = "my_external_id"
        };

        _ = await sutProvider.Sut.Post(orgId, collectionRequest);

        var test = sutProvider.GetDependency<ICollectionService>().ReceivedCalls();
        await sutProvider.GetDependency<ICollectionService>()
            .Received(1)
            .SaveAsync(Arg.Any<Collection>(), Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(users => !users.Any()));
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
            .GetByIdAsync(collectionId, userId, Arg.Any<bool>())
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
            .GetByIdAsync(collectionId, userId, Arg.Any<bool>())
            .Returns(Task.FromResult<CollectionDetails>(null));

        _ = await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.Put(orgId, collectionId, collectionRequest));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_NoManagerPermissions_ThrowsNotFound(Organization organization, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetManyWithDetails(organization.Id));
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByOrganizationIdWithAccessAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByUserIdWithAccessAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_AdminPermissions_GetsAllCollections(Organization organization, User user, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAllCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(true);

        await sutProvider.Sut.GetManyWithDetails(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyByOrganizationIdWithAccessAsync(organization.Id);
        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyByUserIdWithAccessAsync(user.Id, organization.Id, Arg.Any<bool>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_MissingViewAllPermissions_GetsAssignedCollections(Organization organization, User user, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationManager(organization.Id).Returns(true);

        await sutProvider.Sut.GetManyWithDetails(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByOrganizationIdWithAccessAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyByUserIdWithAccessAsync(user.Id, organization.Id, Arg.Any<bool>());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_CustomUserWithManagerPermissions_GetsAssignedCollections(Organization organization, User user, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().EditAssignedCollections(organization.Id).Returns(true);


        await sutProvider.Sut.GetManyWithDetails(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByOrganizationIdWithAccessAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyByUserIdWithAccessAsync(user.Id, organization.Id, Arg.Any<bool>());
    }


    [Theory, BitAutoData]
    public async Task DeleteMany_Success(Guid orgId, User user, Collection collection1, Collection collection2, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        var model = new CollectionBulkDeleteRequestModel
        {
            Ids = new[] { collection1.Id, collection2.Id },
        };

        var collections = new List<Collection>
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

        sutProvider.GetDependency<ICollectionService>()
            .GetOrganizationCollectionsAsync(orgId)
            .Returns(collections);

        // Act
        await sutProvider.Sut.DeleteMany(orgId, model);

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
            Ids = new[] { collection1.Id, collection2.Id },
        };

        sutProvider.GetDependency<ICurrentContext>()
            .DeleteAssignedCollections(orgId)
            .Returns(false);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteMany(orgId, model));

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
            Ids = new[] { collection1.Id, collection2.Id },
        };

        var collections = new List<Collection>
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

        sutProvider.GetDependency<ICollectionService>()
            .GetOrganizationCollectionsAsync(orgId)
            .Returns(collections);

        // Act
        await sutProvider.Sut.DeleteMany(orgId, model);

        // Assert
        await sutProvider.GetDependency<IDeleteCollectionCommand>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Collection>>(coll => coll.Select(c => c.Id).SequenceEqual(collections.Select(c => c.Id))));
    }


}
