using System.Security.Claims;
using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(CollectionsController))]
[SutProviderCustomize]
public class CollectionsControllerTests
{
    [Theory, BitAutoData]
    public async Task Post_Success(OrganizationAbility organizationAbility, CollectionRequestModel collectionRequest,
        SutProvider<CollectionsController> sutProvider)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);

        Collection ExpectedCollection() => Arg.Is<Collection>(c =>
            c.Name == collectionRequest.Name && c.ExternalId == collectionRequest.ExternalId &&
            c.OrganizationId == organizationAbility.Id);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                ExpectedCollection(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Success());

        _ = await sutProvider.Sut.Post(organizationAbility.Id, collectionRequest);

        await sutProvider.GetDependency<ICollectionService>()
            .Received(1)
            .SaveAsync(Arg.Any<Collection>(), Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>());
    }

    [Theory, BitAutoData]
    public async Task Put_Success(Collection collection, CollectionRequestModel collectionRequest,
        SutProvider<CollectionsController> sutProvider, OrganizationAbility organizationAbility)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);
        collection.OrganizationId = organizationAbility.Id;

        Collection ExpectedCollection() => Arg.Is<Collection>(c => c.Id == collection.Id &&
            c.Name == collectionRequest.Name && c.ExternalId == collectionRequest.ExternalId &&
            c.OrganizationId == collection.OrganizationId);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdAsync(collection.Id)
            .Returns(collection);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                collection,
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(BulkCollectionOperations.Update)))
            .Returns(AuthorizationResult.Success());

        _ = await sutProvider.Sut.Put(collection.OrganizationId, collection.Id, collectionRequest);

        await sutProvider.GetDependency<ICollectionService>()
            .Received(1)
            .SaveAsync(ExpectedCollection(), Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>());
    }

    [Theory, BitAutoData]
    public async Task Put_WithNoCollectionPermission_ThrowsNotFound(Collection collection, CollectionRequestModel collectionRequest,
        SutProvider<CollectionsController> sutProvider, OrganizationAbility organizationAbility)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);
        collection.OrganizationId = organizationAbility.Id;

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                collection,
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(BulkCollectionOperations.Update)))
            .Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdAsync(collection.Id)
            .Returns(collection);

        _ = await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.Put(collection.OrganizationId, collection.Id, collectionRequest));
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_WithReadAllPermissions_GetsAllCollections(OrganizationAbility organizationAbility,
        Guid userId, SutProvider<CollectionsController> sutProvider)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Cast<CollectionOperationRequirement>().All(operation =>
                        operation.Name == nameof(CollectionOperations.ReadAllWithAccess)
                        && operation.OrganizationId == organizationAbility.Id)))
            .Returns(AuthorizationResult.Success());

        await sutProvider.Sut.GetManyWithDetails(organizationAbility.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByUserIdWithAccessAsync(userId, organizationAbility.Id, Arg.Any<bool>());
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByOrganizationIdWithAccessAsync(organizationAbility.Id);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_MissingReadAllPermissions_GetsAssignedCollections(
        OrganizationAbility organizationAbility, Guid userId, SutProvider<CollectionsController> sutProvider)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Cast<CollectionOperationRequirement>().All(operation =>
                        operation.Name == nameof(CollectionOperations.ReadAllWithAccess)
                        && operation.OrganizationId == organizationAbility.Id)))
            .Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Cast<BulkCollectionOperationRequirement>().All(operation =>
                        operation.Name == nameof(BulkCollectionOperations.Read) ||
                            operation.Name == nameof(BulkCollectionOperations.ReadAccess))))
            .Returns(AuthorizationResult.Success());

        await sutProvider.Sut.GetManyWithDetails(organizationAbility.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByUserIdWithAccessAsync(userId, organizationAbility.Id, Arg.Any<bool>());
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByOrganizationIdWithAccessAsync(organizationAbility.Id);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollections_WithReadAllPermissions_GetsAllCollections(
        OrganizationAbility organizationAbility, List<Collection> collections, Guid userId, SutProvider<CollectionsController> sutProvider)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);
        collections.ForEach(c => c.OrganizationId = organizationAbility.Id);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Cast<CollectionOperationRequirement>().All(operation =>
                        operation.Name == nameof(CollectionOperations.ReadAll)
                        && operation.OrganizationId == organizationAbility.Id)))
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(organizationAbility.Id)
            .Returns(collections);

        var response = await sutProvider.Sut.Get(organizationAbility.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByOrganizationIdAsync(organizationAbility.Id);

        Assert.Equal(collections.Count, response.Data.Count());
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollections_MissingReadAllPermissions_GetsManageableCollections(
        OrganizationAbility organizationAbility, List<CollectionDetails> collections, Guid userId, SutProvider<CollectionsController> sutProvider)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);
        collections.ForEach(c => c.OrganizationId = organizationAbility.Id);
        collections.ForEach(c => c.Manage = false);

        var managedCollection = collections.First();
        managedCollection.Manage = true;

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Cast<CollectionOperationRequirement>().All(operation =>
                        operation.Name == nameof(CollectionOperations.ReadAll)
                        && operation.OrganizationId == organizationAbility.Id)))
            .Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Cast<BulkCollectionOperationRequirement>().All(operation =>
                        operation.Name == nameof(BulkCollectionOperations.Read))))
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByUserIdAsync(userId, false)
            .Returns(collections);

        var result = await sutProvider.Sut.Get(organizationAbility.Id);

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByOrganizationIdAsync(organizationAbility.Id);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByUserIdAsync(userId, false);

        Assert.Single(result.Data);
        Assert.All(result.Data, c => Assert.Equal(organizationAbility.Id, c.OrganizationId));
        Assert.All(result.Data, c => Assert.Equal(managedCollection.Id, c.Id));
    }

    [Theory, BitAutoData]
    public async Task DeleteMany_Success(OrganizationAbility organizationAbility, Collection collection1, Collection collection2,
         SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        var orgId = organizationAbility.Id;
        ArrangeOrganizationAbility(sutProvider, organizationAbility);

        var model = new CollectionBulkDeleteRequestModel
        {
            Ids = new[] { collection1.Id, collection2.Id }
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

        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                collections,
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(BulkCollectionOperations.Delete)))
            .Returns(AuthorizationResult.Success());

        // Act
        await sutProvider.Sut.DeleteMany(orgId, model);

        // Assert
        await sutProvider.GetDependency<IDeleteCollectionCommand>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Collection>>(coll => coll.Select(c => c.Id).SequenceEqual(collections.Select(c => c.Id))));

    }

    [Theory, BitAutoData]
    public async Task DeleteMany_PermissionDenied_ThrowsNotFound(OrganizationAbility organizationAbility, Collection collection1,
        Collection collection2, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        var orgId = organizationAbility.Id;
        ArrangeOrganizationAbility(sutProvider, organizationAbility);

        var model = new CollectionBulkDeleteRequestModel
        {
            Ids = new[] { collection1.Id, collection2.Id }
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

        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                collections,
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(BulkCollectionOperations.Delete)))
            .Returns(AuthorizationResult.Failed());

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteMany(orgId, model));

        await sutProvider.GetDependency<IDeleteCollectionCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync((IEnumerable<Collection>)default);
    }

    [Theory, BitAutoData]
    public async Task PostBulkCollectionAccess_Success(User actingUser, List<Collection> collections,
        OrganizationAbility organizationAbility, SutProvider<CollectionsController> sutProvider)
    {
        // Arrange
        ArrangeOrganizationAbility(sutProvider, organizationAbility);
        collections.ForEach(c => c.OrganizationId = organizationAbility.Id);

        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var model = new BulkCollectionAccessRequestModel
        {
            CollectionIds = collections.Select(c => c.Id),
            Users = new[] { new SelectionReadOnlyRequestModel { Id = userId, Manage = true } },
            Groups = new[] { new SelectionReadOnlyRequestModel { Id = groupId, ReadOnly = true } },
        };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(model.CollectionIds)
            .Returns(collections);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(actingUser.Id);

        sutProvider.GetDependency<IAuthorizationService>().AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(), ExpectedCollectionAccess(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                    r => r.Contains(BulkCollectionOperations.ModifyAccess)
                ))
            .Returns(AuthorizationResult.Success());

        IEnumerable<Collection> ExpectedCollectionAccess() => Arg.Is<IEnumerable<Collection>>(cols => cols.SequenceEqual(collections));

        // Act
        await sutProvider.Sut.PostBulkCollectionAccess(organizationAbility.Id, model);

        // Assert
        await sutProvider.GetDependency<IAuthorizationService>().Received().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            ExpectedCollectionAccess(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                r => r.Contains(BulkCollectionOperations.ModifyAccess))
            );
        await sutProvider.GetDependency<IBulkAddCollectionAccessCommand>().Received()
            .AddAccessAsync(
                Arg.Is<ICollection<Collection>>(g => g.SequenceEqual(collections)),
                Arg.Is<ICollection<CollectionAccessSelection>>(u => u.All(c => c.Id == userId && c.Manage)),
                Arg.Is<ICollection<CollectionAccessSelection>>(g => g.All(c => c.Id == groupId && c.ReadOnly)));
    }

    [Theory, BitAutoData]
    public async Task PostBulkCollectionAccess_CollectionsNotFound_Throws(User actingUser,
        OrganizationAbility organizationAbility, List<Collection> collections,
        SutProvider<CollectionsController> sutProvider)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);
        collections.ForEach(c => c.OrganizationId = organizationAbility.Id);

        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var model = new BulkCollectionAccessRequestModel
        {
            CollectionIds = collections.Select(c => c.Id),
            Users = new[] { new SelectionReadOnlyRequestModel { Id = userId, Manage = true } },
            Groups = new[] { new SelectionReadOnlyRequestModel { Id = groupId, ReadOnly = true } },
        };

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(actingUser.Id);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(model.CollectionIds)
            .Returns(collections.Skip(1).ToList());

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PostBulkCollectionAccess(organizationAbility.Id, model));

        Assert.Equal("One or more collections not found.", exception.Message);
        await sutProvider.GetDependency<IAuthorizationService>().DidNotReceiveWithAnyArgs().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<IEnumerable<Collection>>(),
            Arg.Any<IEnumerable<IAuthorizationRequirement>>()
        );
        await sutProvider.GetDependency<IBulkAddCollectionAccessCommand>().DidNotReceiveWithAnyArgs()
            .AddAccessAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task PostBulkCollectionAccess_CollectionsBelongToDifferentOrganizations_Throws(User actingUser,
        OrganizationAbility organizationAbility, List<Collection> collections,
        SutProvider<CollectionsController> sutProvider)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);

        // First collection has a different orgId
        collections.Skip(1).ToList().ForEach(c => c.OrganizationId = organizationAbility.Id);

        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var model = new BulkCollectionAccessRequestModel
        {
            CollectionIds = collections.Select(c => c.Id),
            Users = new[] { new SelectionReadOnlyRequestModel { Id = userId, Manage = true } },
            Groups = new[] { new SelectionReadOnlyRequestModel { Id = groupId, ReadOnly = true } },
        };

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(actingUser.Id);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(model.CollectionIds)
            .Returns(collections);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PostBulkCollectionAccess(organizationAbility.Id, model));

        Assert.Equal("One or more collections not found.", exception.Message);
        await sutProvider.GetDependency<IAuthorizationService>().DidNotReceiveWithAnyArgs().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<IEnumerable<Collection>>(),
            Arg.Any<IEnumerable<IAuthorizationRequirement>>()
        );
        await sutProvider.GetDependency<IBulkAddCollectionAccessCommand>().DidNotReceiveWithAnyArgs()
            .AddAccessAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task PostBulkCollectionAccess_FlexibleCollectionsDisabled_Throws(OrganizationAbility organizationAbility, List<Collection> collections,
        SutProvider<CollectionsController> sutProvider)
    {
        organizationAbility.FlexibleCollections = false;
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);

        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var model = new BulkCollectionAccessRequestModel
        {
            CollectionIds = collections.Select(c => c.Id),
            Users = new[] { new SelectionReadOnlyRequestModel { Id = userId, Manage = true } },
            Groups = new[] { new SelectionReadOnlyRequestModel { Id = groupId, ReadOnly = true } },
        };

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PostBulkCollectionAccess(organizationAbility.Id, model));

        Assert.Equal("Feature disabled.", exception.Message);
        await sutProvider.GetDependency<IAuthorizationService>().DidNotReceiveWithAnyArgs().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<IEnumerable<Collection>>(),
            Arg.Any<IEnumerable<IAuthorizationRequirement>>()
        );
        await sutProvider.GetDependency<IBulkAddCollectionAccessCommand>().DidNotReceiveWithAnyArgs()
            .AddAccessAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task PostBulkCollectionAccess_AccessDenied_Throws(User actingUser, List<Collection> collections,
        OrganizationAbility organizationAbility, SutProvider<CollectionsController> sutProvider)
    {
        ArrangeOrganizationAbility(sutProvider, organizationAbility);
        collections.ForEach(c => c.OrganizationId = organizationAbility.Id);

        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var model = new BulkCollectionAccessRequestModel
        {
            CollectionIds = collections.Select(c => c.Id),
            Users = new[] { new SelectionReadOnlyRequestModel { Id = userId, Manage = true } },
            Groups = new[] { new SelectionReadOnlyRequestModel { Id = groupId, ReadOnly = true } },
        };

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(actingUser.Id);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(model.CollectionIds)
            .Returns(collections);

        sutProvider.GetDependency<IAuthorizationService>().AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(), ExpectedCollectionAccess(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                    r => r.Contains(BulkCollectionOperations.ModifyAccess)
                ))
            .Returns(AuthorizationResult.Failed());

        IEnumerable<Collection> ExpectedCollectionAccess() => Arg.Is<IEnumerable<Collection>>(cols => cols.SequenceEqual(collections));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostBulkCollectionAccess(organizationAbility.Id, model));
        await sutProvider.GetDependency<IAuthorizationService>().Received().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            ExpectedCollectionAccess(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                r => r.Contains(BulkCollectionOperations.ModifyAccess))
            );
        await sutProvider.GetDependency<IBulkAddCollectionAccessCommand>().DidNotReceiveWithAnyArgs()
            .AddAccessAsync(default, default, default);
    }

    private void ArrangeOrganizationAbility(SutProvider<CollectionsController> sutProvider, OrganizationAbility organizationAbility)
    {
        organizationAbility.FlexibleCollections = true;

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);
    }
}
