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
public class CollectionUserAuthorizationHandlerTests
{
    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<OrganizationUser> users,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.Create },
            new ClaimsPrincipal(),
            collectionUsers
            );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_MissingUserId_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<OrganizationUser> users,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails)
    {
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.Create },
            new ClaimsPrincipal(),
            collectionUsers
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_MissingTargetCollection_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        IList<Collection> collections,
        ICollection<OrganizationUser> users,
        ICollection<CollectionUser> collectionUsers,
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
            new[] { CollectionUserOperation.Create },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_MissingManageCollectionPermission_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<OrganizationUser> users,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();

        // Simulate a collection without manage permission
        collectionDetails.First().Manage = false;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.Create },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_MissingTargetUser_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        IList<OrganizationUser> users,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        // Simulate a missing target user
        users.RemoveAt(0);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.Create },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_WrongOrgForTargetUser_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<OrganizationUser> users,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        // Simulate a user in a different organization
        users.First().OrganizationId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.Create },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.Delete },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_MissingUserId_Failure(
    SutProvider<CollectionUserAuthorizationHandler> sutProvider,
    ICollection<Collection> collections,
    ICollection<CollectionUser> collectionUsers,
    ICollection<CollectionDetails> collectionDetails)
    {
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.Delete },
            new ClaimsPrincipal(),
            collectionUsers
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
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        IList<Collection> collections,
        ICollection<CollectionUser> collectionUsers,
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
            new[] { CollectionUserOperation.Delete },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_MissingManageCollectionPermission_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails)
    {
        var actingUserId = Guid.NewGuid();

        // Simulate a collection without manage permission
        collectionDetails.First().Manage = false;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.Delete },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(Arg.Any<Guid>()).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_Success(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionUser> collectionUsers,
        ICollection<OrganizationUser> users)
    {
        // Ensure all collection users have the same collection id
        foreach (var cu in collectionUsers)
        {
            cu.CollectionId = collection.Id;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.CreateForNewCollection(collection) },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_MissingRequirement_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionUser> collectionUsers,
        ICollection<OrganizationUser> users)
    {
        // Ensure all collection users have the same collection id
        foreach (var cu in collectionUsers)
        {
            cu.CollectionId = collection.Id;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.CreateForNewCollection(null) },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_WrongCollectionId_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionUser> collectionUsers,
        ICollection<OrganizationUser> users)
    {
        // Ensure all collection users have the same collection id
        foreach (var cu in collectionUsers)
        {
            cu.CollectionId = collection.Id;
        }

        // Simulate a wrong collection id
        collectionUsers.First().CollectionId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.CreateForNewCollection(collection) },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_MissingTargetUser_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionUser> collectionUsers,
        IList<OrganizationUser> users)
    {
        // Ensure all collection users have the same collection id
        foreach (var cu in collectionUsers)
        {
            cu.CollectionId = collection.Id;
        }

        // Simulate a missing target user
        users.RemoveAt(0);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.CreateForNewCollection(collection) },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateForNewCollectionAsync_WrongOrgForTargetUser_Failure(
        SutProvider<CollectionUserAuthorizationHandler> sutProvider,
        Collection collection,
        ICollection<CollectionUser> collectionUsers,
        IList<OrganizationUser> users)
    {
        // Ensure all collection users have the same collection id
        foreach (var cu in collectionUsers)
        {
            cu.CollectionId = collection.Id;
        }

        // Simulate a target user that belongs to a different org
        users.First().OrganizationId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionUserOperation.CreateForNewCollection(collection) },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }
}
