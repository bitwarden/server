using System.Security.Claims;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
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

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
public class CollectionAuthorizationHandlerTests
{
    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User, false, true)]
    [BitAutoData(OrganizationUserType.Admin, false, false)]
    [BitAutoData(OrganizationUserType.Owner, false, false)]
    [BitAutoData(OrganizationUserType.Custom, true, false)]
    [BitAutoData(OrganizationUserType.Owner, true, true)]
    public async Task CanManageCollectionAccessAsync_Success(
        OrganizationUserType userType, bool editAnyCollection, bool manageCollections,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionDetails> collectionDetails,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = manageCollections;
        }

        organization.Type = userType;
        organization.Permissions.EditAnyCollection = editAnyCollection;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ModifyAccess },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User, false, false)]
    [BitAutoData(OrganizationUserType.Admin, false, true)]
    [BitAutoData(OrganizationUserType.Owner, false, true)]
    [BitAutoData(OrganizationUserType.Custom, true, true)]
    public async Task CanCreateAsync_Success(
        OrganizationUserType userType, bool createNewCollection, bool limitCollectionCreateDelete,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions.CreateNewCollections = createNewCollection;
        organization.LimitCollectionCdOwnerAdmin = limitCollectionCreateDelete;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User, false, false, true)]
    [BitAutoData(OrganizationUserType.Admin, false, true, false)]
    [BitAutoData(OrganizationUserType.Owner, false, true, false)]
    [BitAutoData(OrganizationUserType.Custom, true, true, false)]
    public async Task CanDeleteAsync_Success(
        OrganizationUserType userType, bool deleteAnyCollection, bool limitCollectionCreateDelete, bool manageCollections,
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionDetails> collectionDetails,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = manageCollections;
        }

        organization.Type = userType;
        organization.Permissions.DeleteAnyCollection = deleteAnyCollection;
        organization.LimitCollectionCdOwnerAdmin = limitCollectionCreateDelete;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
        sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs();
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_TargetCollectionsMultipleOrgs_Failure(
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        IList<Collection> collections)
    {
        var actingUserId = Guid.NewGuid();

        // Simulate a collection in a different organization
        collections.First().OrganizationId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(context));
        Assert.Equal("Requested collections must belong to the same organization.", exception.Message);
        sutProvider.GetDependency<ICurrentContext>().DidNotReceiveWithAnyArgs().GetOrganization(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MissingOrg_Failure(
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        var actingUserId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_MissingManageCollectionPermission_Failure(
        SutProvider<CollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionDetails> collectionDetails,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }
        // Simulate one collection missing the manage permission
        collectionDetails.First().Manage = false;

        // Ensure the user is not an owner/admin and does not have edit any collection permission
        organization.Type = OrganizationUserType.User;
        organization.Permissions.EditAnyCollection = false;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ModifyAccess },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
        sutProvider.GetDependency<ICurrentContext>().ReceivedWithAnyArgs().GetOrganization(default);
        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs()
            .GetManyByUserIdAsync(default);
    }
}
