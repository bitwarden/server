using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Vault.AuthorizationHandlers;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
public class CollectionAccessAuthorizationHandlerTests
{
    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User, false, true)]
    [BitAutoData(OrganizationUserType.Admin, false, false)]
    [BitAutoData(OrganizationUserType.Owner, false, false)]
    [BitAutoData(OrganizationUserType.Custom, true, false)]
    [BitAutoData(OrganizationUserType.Owner, true, true)]
    public async Task CanManageCollectionAccessAsync_Success(
        OrganizationUserType userType, bool editAnyCollection, bool manageCollections,
        SutProvider<CollectionAccessAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails,
        ICollection<CurrentContentOrganization> organizations)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = manageCollections;
        }

        foreach (var org in organizations)
        {
            org.Type = userType;
            org.Permissions.EditAnyCollection = editAnyCollection;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionAccessOperation.CreateDelete },
            new ClaimsPrincipal(),
            collectionUsers
            );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationMembershipAsync(Arg.Any<IOrganizationUserRepository>(), actingUserId).Returns(organizations);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }


    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_MissingUserId_Failure(
        SutProvider<CollectionAccessAuthorizationHandler> sutProvider,
        ICollection<CollectionUser> collectionUsers)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionAccessOperation.CreateDelete },
            new ClaimsPrincipal(),
            collectionUsers
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
        sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs();
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_MissingTargetCollection_Failure(
        SutProvider<CollectionAccessAuthorizationHandler> sutProvider,
        IList<Collection> collections,
        ICollection<CollectionUser> collectionUsers)
    {
        var actingUserId = Guid.NewGuid();

        // Simulate a missing target collection
        collections.RemoveAt(0);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionAccessOperation.CreateDelete },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<ICurrentContext>().DidNotReceiveWithAnyArgs()
            .OrganizationMembershipAsync(default, default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_MissingOrgMembership_Failure(
        SutProvider<CollectionAccessAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CurrentContentOrganization> organizations)
    {
        var actingUserId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionAccessOperation.CreateDelete },
            new ClaimsPrincipal(),
            collectionUsers
        );

        // Simulate a collection that belongs to an unknown organization
        collections.First().OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICurrentContext>().OrganizationMembershipAsync(Arg.Any<IOrganizationUserRepository>(), actingUserId).Returns(organizations);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<ICurrentContext>().ReceivedWithAnyArgs().OrganizationMembershipAsync(default, default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByUserIdAsync(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_MissingManageCollectionPermission_Failure(
        SutProvider<CollectionAccessAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionUser> collectionUsers,
        ICollection<CollectionDetails> collectionDetails,
        ICollection<CurrentContentOrganization> organizations)
    {
        var actingUserId = Guid.NewGuid();

        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }
        // Simulate one collection missing the manage permission
        collectionDetails.First().Manage = false;

        // Ensure the user is not an owner/admin and does not have edit any collection permission
        foreach (var org in organizations)
        {
            org.Type = OrganizationUserType.User;
            org.Permissions.EditAnyCollection = false;
        }

        var context = new AuthorizationHandlerContext(
            new[] { CollectionAccessOperation.CreateDelete },
            new ClaimsPrincipal(),
            collectionUsers
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationMembershipAsync(Arg.Any<IOrganizationUserRepository>(), actingUserId).Returns(organizations);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(collections);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
        await sutProvider.GetDependency<ICurrentContext>().ReceivedWithAnyArgs().OrganizationMembershipAsync(default, default);
        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs().GetManyByManyIdsAsync(default);
        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs()
            .GetManyByUserIdAsync(default);
    }
}
