using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.AuthorizationHandlers;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.AuthorizationHandlers;

[SutProviderCustomize]
public class CollectionGroupAuthorizationHandlerTests
{
    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_Success(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<Group> groups,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Create },
            new ClaimsPrincipal(),
            collectionGroups
            );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_MissingUserId_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<Group> groups,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Create },
            new ClaimsPrincipal(),
            collectionGroups
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_MissingTargetCollection_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        IList<Collection> collections,
        ICollection<Group> groups,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        // Simulate a missing target collection
        collections.RemoveAt(0);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Create },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_MissingManageCollectionPermission_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<Group> groups,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();

        // Simulate a collection without manage permission
        collectionDetails.First().Manage = false;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Create },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_MissingTargetUser_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        IList<Group> groups,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        // Simulate a missing target user
        groups.RemoveAt(0);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Create },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_WrongOrgForTargetUser_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<Group> groups,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        // Simulate a user in a different organization
        groups.First().OrganizationId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Create },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_Success(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Delete },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_MissingUserId_Failure(
    SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
    ICollection<Collection> collections,
    ICollection<CollectionGroup> collectionGroups,
    ICollection<CollectionDetails> collectionDetails)
    {
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Delete },
            new ClaimsPrincipal(),
            collectionGroups
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_MissingTargetCollection_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        IList<Collection> collections,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        // Simulate a missing target collection
        collections.RemoveAt(0);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Delete },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_MissingManageCollectionPermission_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();

        // Simulate a collection without manage permission
        collectionDetails.First().Manage = false;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.Delete },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_Success(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<Group> groups)
    {
        // Ensure all collection groups have the same collection id
        foreach (var cu in collectionGroups)
        {
            cu.CollectionId = collection.Id;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.CreateForNewCollection(collection) },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_MissingRequirement_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<Group> groups)
    {
        // Ensure all collection groups have the same collection id
        foreach (var cu in collectionGroups)
        {
            cu.CollectionId = collection.Id;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.CreateForNewCollection(null) },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_WrongCollectionId_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionGroup> collectionGroups,
        ICollection<Group> groups)
    {
        // Ensure all collection groups have the same collection id
        foreach (var cu in collectionGroups)
        {
            cu.CollectionId = collection.Id;
        }

        // Simulate a wrong collection id
        collectionGroups.First().CollectionId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.CreateForNewCollection(collection) },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_MissingTargetUser_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionGroup> collectionGroups,
        IList<Group> groups)
    {
        // Ensure all collection groups have the same collection id
        foreach (var cu in collectionGroups)
        {
            cu.CollectionId = collection.Id;
        }

        // Simulate a missing target user
        groups.RemoveAt(0);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.CreateForNewCollection(collection) },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_WrongOrgForTargetUser_Failure(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionGroup> collectionGroups,
        IList<Group> groups)
    {
        // Ensure all collection groups have the same collection id
        foreach (var cu in collectionGroups)
        {
            cu.CollectionId = collection.Id;
        }

        // Simulate a target user that belongs to a different org
        groups.First().OrganizationId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperation.CreateForNewCollection(collection) },
            new ClaimsPrincipal(),
            collectionGroups
        );

        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns(groups);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }
}
