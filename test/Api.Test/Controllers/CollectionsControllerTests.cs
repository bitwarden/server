using System.Security.Claims;
using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
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
    public async Task Post_Success(Guid orgId, CollectionRequestModel collectionRequest,
        SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);

        Collection ExpectedCollection() => Arg.Is<Collection>(c =>
            c.Name == collectionRequest.Name && c.ExternalId == collectionRequest.ExternalId &&
            c.OrganizationId == orgId);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                ExpectedCollection(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(CollectionOperations.Create)))
            .Returns(AuthorizationResult.Success());

        _ = await sutProvider.Sut.Post(orgId, collectionRequest);

        await sutProvider.GetDependency<ICollectionService>()
            .Received(1)
            .SaveAsync(Arg.Any<Collection>(), Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>());
    }

    [Theory, BitAutoData]
    public async Task Put_Success(Guid orgId, Guid collectionId, Guid userId, CollectionRequestModel collectionRequest,
        SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
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
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
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
    public async Task GetOrganizationCollectionsWithGroups_NoManagerPermissions_ThrowsNotFound(Organization organization, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetManyWithDetails(organization.Id));
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByOrganizationIdWithAccessAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByUserIdWithAccessAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_AdminPermissions_GetsAllCollections(Organization organization, User user, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAllCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(true);

        await sutProvider.Sut.GetManyWithDetails(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyByOrganizationIdWithAccessAsync(organization.Id);
        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyByUserIdWithAccessAsync(user.Id, organization.Id);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_MissingViewAllPermissions_GetsAssignedCollections(Organization organization, User user, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationManager(organization.Id).Returns(true);

        await sutProvider.Sut.GetManyWithDetails(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByOrganizationIdWithAccessAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyByUserIdWithAccessAsync(user.Id, organization.Id);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationCollectionsWithGroups_CustomUserWithManagerPermissions_GetsAssignedCollections(Organization organization, User user, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<ICurrentContext>().ViewAssignedCollections(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().EditAssignedCollections(organization.Id).Returns(true);


        await sutProvider.Sut.GetManyWithDetails(organization.Id);

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().GetManyByOrganizationIdWithAccessAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().Received().GetManyByUserIdWithAccessAsync(user.Id, organization.Id);
    }


    [Theory, BitAutoData]
    public async Task DeleteMany_Success(Guid orgId, Collection collection1, Collection collection2, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
        // Arrange
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
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(CollectionOperations.Delete)))
            .Returns(AuthorizationResult.Success());

        // Act
        await sutProvider.Sut.DeleteMany(orgId, model);

        // Assert
        await sutProvider.GetDependency<IDeleteCollectionCommand>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Collection>>(coll => coll.Select(c => c.Id).SequenceEqual(collections.Select(c => c.Id))));

    }

    [Theory, BitAutoData]
    public async Task DeleteMany_PermissionDenied_ThrowsNotFound(Guid orgId, Collection collection1, Collection collection2, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
        // Arrange
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
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(CollectionOperations.Delete)))
            .Returns(AuthorizationResult.Failed());

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteMany(orgId, model));

        await sutProvider.GetDependency<IDeleteCollectionCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync((IEnumerable<Collection>)default);
    }

    [Theory, BitAutoData]
    public async Task PostBulkCollectionAccess_Success(User actingUser, ICollection<Collection> collections, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
        // Arrange
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
                    r => r.Contains(CollectionOperations.ModifyAccess)
                ))
            .Returns(AuthorizationResult.Success());

        IEnumerable<Collection> ExpectedCollectionAccess() => Arg.Is<IEnumerable<Collection>>(cols => cols.SequenceEqual(collections));

        // Act
        await sutProvider.Sut.PostBulkCollectionAccess(model);

        // Assert
        await sutProvider.GetDependency<IAuthorizationService>().Received().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            ExpectedCollectionAccess(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                r => r.Contains(CollectionOperations.ModifyAccess))
            );
        await sutProvider.GetDependency<IBulkAddCollectionAccessCommand>().Received()
            .AddAccessAsync(
                Arg.Is<ICollection<Collection>>(g => g.SequenceEqual(collections)),
                Arg.Is<ICollection<CollectionAccessSelection>>(u => u.All(c => c.Id == userId && c.Manage)),
                Arg.Is<ICollection<CollectionAccessSelection>>(g => g.All(c => c.Id == groupId && c.ReadOnly)));
    }

    [Theory, BitAutoData]
    public async Task PostBulkCollectionAccess_CollectionsNotFound_Throws(User actingUser, ICollection<Collection> collections, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
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

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostBulkCollectionAccess(model));

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
    public async Task PostBulkCollectionAccess_AccessDenied_Throws(User actingUser, ICollection<Collection> collections, SutProvider<CollectionsController> sutProvider)
    {
        sutProvider.EnableFeatureFlag(FeatureFlagKeys.FlexibleCollections);
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
                    r => r.Contains(CollectionOperations.ModifyAccess)
                ))
            .Returns(AuthorizationResult.Failed());

        IEnumerable<Collection> ExpectedCollectionAccess() => Arg.Is<IEnumerable<Collection>>(cols => cols.SequenceEqual(collections));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostBulkCollectionAccess(model));
        await sutProvider.GetDependency<IAuthorizationService>().Received().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            ExpectedCollectionAccess(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                r => r.Contains(CollectionOperations.ModifyAccess))
            );
        await sutProvider.GetDependency<IBulkAddCollectionAccessCommand>().DidNotReceiveWithAnyArgs()
            .AddAccessAsync(default, default, default);
    }
}
