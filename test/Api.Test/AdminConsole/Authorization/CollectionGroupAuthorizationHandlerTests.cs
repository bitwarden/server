using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class CollectionGroupAuthorizationHandlerTests
{
    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MissingUserId_NoSuccess(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        CollectionGroupAccessResource resource)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update }, new ClaimsPrincipal(), resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WithEditAnyCollectionPermission_Success(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        CollectionGroupAccessResource resource,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Permissions = new Permissions { EditAnyCollection = true };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(resource.Collection.OrganizationId).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update }, new ClaimsPrincipal(), resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WhenMissingPermissions_NoSuccess(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        CollectionGroupAccessResource resource,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(resource.Collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update }, new ClaimsPrincipal(), resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_WhenProviderUser_Success(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        CollectionGroupAccessResource resource,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(resource.Collection.OrganizationId).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(resource.Collection.OrganizationId).Returns(true);

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update }, new ClaimsPrincipal(), resource);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_CalledTwiceForSameResource_OnlyQueriesRepositoryOnce(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var addResource = new CollectionGroupAccessResource(collection, accessDetails);
        var removeResource = new CollectionGroupAccessResource(collection, accessDetails);

        var context1 = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Create }, new ClaimsPrincipal(), addResource);
        await sutProvider.Sut.HandleAsync(context1);

        var context2 = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Delete }, new ClaimsPrincipal(), removeResource);
        await sutProvider.Sut.HandleAsync(context2);

        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MultipleResources_AllAuthorized_Success(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Guid organizationId,
        CollectionAccessDetails accessDetailsA,
        CollectionAccessDetails accessDetailsB,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Permissions = new Permissions { EditAnyCollection = true };
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);

        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId };
        var resources = new[]
        {
            new CollectionGroupAccessResource(collectionA, accessDetailsA),
            new CollectionGroupAccessResource(collectionB, accessDetailsB)
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update }, new ClaimsPrincipal(), resources);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MultipleResources_OneUnauthorized_NoSuccess(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        Guid organizationId,
        CollectionAccessDetails accessDetailsA,
        CollectionAccessDetails accessDetailsB,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(false);
        // Caller manages collectionA (directly) but not collectionB.
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails> { new() { Id = collectionA.Id, Manage = true } });

        var resources = new[]
        {
            new CollectionGroupAccessResource(collectionA, accessDetailsA),
            new CollectionGroupAccessResource(collectionB, accessDetailsB)
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update }, new ClaimsPrincipal(), resources);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MultipleResources_DifferentOrganizations_Throws(
        SutProvider<CollectionGroupAuthorizationHandler> sutProvider,
        CollectionAccessDetails accessDetailsA,
        CollectionAccessDetails accessDetailsB,
        Guid userId)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() };
        var resources = new[]
        {
            new CollectionGroupAccessResource(collectionA, accessDetailsA),
            new CollectionGroupAccessResource(collectionB, accessDetailsB)
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionGroupOperations.Update }, new ClaimsPrincipal(), resources);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(context));
    }
}
