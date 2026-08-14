using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class CollectionAuthorizationServiceTests
{
    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_CollectionNotFound_AllUnauthorized(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_CollectionBelongsToDifferentOrganization_AllUnauthorized(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        Guid organizationId)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collection.Id);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_MissingUserId_AllUnauthorized(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WithEditAnyCollectionPermission_FullyAuthorized(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Permissions = new Permissions { EditAnyCollection = true };
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.CanUpdateCollection);
        Assert.True(result.CanModifyUserAccess);
        Assert.True(result.CanModifyGroupAccess);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenMissingPermissions_AllUnauthorized(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenProviderUser_FullyAuthorized(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        Guid userId)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns((CurrentContextOrganization?)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(collection.OrganizationId).Returns(true);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_ManageUsersPermission_AllowAdminAccessTrue_UpdateStaysUnauthorized(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        // ManageUsers/AllowAdminAccessToAllCollectionItems is a bypass for CanModifyUserAccess only - it
        // must not also authorize the collection's own metadata update.
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result.CanUpdateCollection);
        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenCallerManagesCollection_FullyAuthorized(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails> { new() { Id = collection.Id, Manage = true } });

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result.IsSuccess);
    }
}
