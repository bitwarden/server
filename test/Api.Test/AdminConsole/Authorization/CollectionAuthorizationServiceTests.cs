using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
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
    public async Task AuthorizeUpdateAsync_WhenCollectionNotFound_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId)
    {
        SetupCollections(sutProvider);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenCollectionBelongsToDifferentOrganization_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        Guid organizationId)
    {
        SetupCollections(sutProvider, collection);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collection.Id);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenMissingUserId_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection)
    {
        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WithEditAnyCollectionPermission_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Permissions = new Permissions { EditAnyCollection = true };
        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByUserIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive()
            .GetManyByOrganizationIdWithAccessAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenMissingPermissions_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
        // Only Owners and Admins can be authorized by an orphaned collection, so the query is not needed here.
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive()
            .GetManyByOrganizationIdWithAccessAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenProviderUser_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        Guid userId)
    {
        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns((CurrentContextOrganization?)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(collection.OrganizationId).Returns(true);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenNotOrganizationMember_SkipsMemberOnlyLookups(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        Guid userId)
    {
        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns((CurrentContextOrganization?)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(collection.OrganizationId).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
        await sutProvider.GetDependency<IOrganizationAbilityCacheService>().DidNotReceive()
            .GetOrganizationAbilityAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByUserIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive()
            .GetManyByOrganizationIdWithAccessAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WithManageUsersPermission_WhenAllowAdminAccessTrue_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        // ManageUsers with AllowAdminAccessToAllCollectionItems authorizes CanModifyUserAccess only. It must
        // not authorize an update to the collection metadata.
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenCallerManagesCollection_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails> { new() { Id = collection.Id, Manage = true } });

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenOwnerManagesCollectionDirectly_SkipsOrphanedCollectionsQuery(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails> { new() { Id = collection.Id, Manage = true } });

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive()
            .GetManyByOrganizationIdWithAccessAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenOwnerAndCollectionOrphaned_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, collection);
        SetupOrganizationCollectionAccess(sutProvider, collection.OrganizationId,
            (collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    // The orphaned check is the same for all three operations. Each half of the condition (a user manages the
    // collection, or a group manages it) is therefore covered once, not once per operation.
    [Theory, BitAutoData, CollectionCustomization]
    public async Task WhenOwnerAndCollectionManagedByUser_NotOrphaned_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId,
        CollectionAccessSelection collectionManager)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();
        collectionManager.Manage = true;

        SetupCollections(sutProvider, collection);
        SetupOrganizationCollectionAccess(sutProvider, collection.OrganizationId,
            (collection, new CollectionAccessDetails { Users = [collectionManager], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WhenEmptyRequest_ReturnsEmpty(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId)
    {
        var result = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(organizationId, []);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WhenCollectionNotFound_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId)
    {
        SetupCollections(sutProvider);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(organizationId, [collectionId]);

        Assert.Empty(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WithMixedAuthorization_ReturnsOnlyAuthorizedIds(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection authorizedCollection,
        Collection unauthorizedCollection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        // Two collections in the same request. Only the collection that the caller manages is authorized.
        unauthorizedCollection.OrganizationId = authorizedCollection.OrganizationId;
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, authorizedCollection, unauthorizedCollection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(authorizedCollection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails> { new() { Id = authorizedCollection.Id, Manage = true } });

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(
            authorizedCollection.OrganizationId, [authorizedCollection.Id, unauthorizedCollection.Id]);

        Assert.Equal(new HashSet<Guid> { authorizedCollection.Id }, result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WhenCalledTwice_ReusesFetchedFacts(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        // The cached data is scoped to the caller or to the organization, so a second authorization in the
        // same request must not query it again.
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, collection);
        SetupOrganizationCollectionAccess(sutProvider, collection.OrganizationId,
            (collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var firstResult = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(collection.OrganizationId, [collection.Id]);
        var secondResult = await sutProvider.Sut.AuthorizeModifyGroupAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, firstResult);
        Assert.Equal(new HashSet<Guid> { collection.Id }, secondResult);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByUserIdAsync(userId);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .GetManyByOrganizationIdWithAccessAsync(collection.OrganizationId);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WithEditAnyCollectionPermission_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Permissions = new Permissions { EditAnyCollection = true };
        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WhenProviderUser_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        Guid userId)
    {
        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns((CurrentContextOrganization?)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(collection.OrganizationId).Returns(true);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WhenOwnerAndCollectionOrphaned_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, collection);
        SetupOrganizationCollectionAccess(sutProvider, collection.OrganizationId,
            (collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WithManageUsersPermission_WhenAllowAdminAccessTrue_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        // ManageUsers with AllowAdminAccessToAllCollectionItems authorizes a change to user access, even
        // though it does not authorize an update to the collection metadata.
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WithManageUsersPermission_WhenAllowAdminAccessFalse_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Empty(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WhenCollectionNotFound_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId)
    {
        SetupCollections(sutProvider);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessManyAsync(organizationId, [collectionId]);

        Assert.Empty(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WithEditAnyCollectionPermission_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Permissions = new Permissions { EditAnyCollection = true };
        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WhenProviderUser_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        Guid userId)
    {
        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns((CurrentContextOrganization?)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(collection.OrganizationId).Returns(true);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WhenOwnerAndCollectionOrphaned_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, collection);
        SetupOrganizationCollectionAccess(sutProvider, collection.OrganizationId,
            (collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task WhenOwnerAndCollectionManagedByGroup_NotOrphaned_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId,
        CollectionAccessSelection collectionManager)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();
        collectionManager.Manage = true;

        SetupCollections(sutProvider, collection);
        SetupOrganizationCollectionAccess(sutProvider, collection.OrganizationId,
            (collection, new CollectionAccessDetails { Users = [], Groups = [collectionManager] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Empty(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WithManageGroupsPermission_WhenAllowAdminAccessTrue_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        // ManageGroups with AllowAdminAccessToAllCollectionItems authorizes a change to group access, even
        // though it does not authorize an update to the collection metadata.
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Equal(new HashSet<Guid> { collection.Id }, result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WithManageGroupsPermission_WhenAllowAdminAccessFalse_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        SetupCollections(sutProvider, collection);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessManyAsync(collection.OrganizationId, [collection.Id]);

        Assert.Empty(result);
    }

    private static void SetupCollections(
        SutProvider<CollectionAuthorizationService> sutProvider,
        params Collection[] collections)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections.ToList());
    }

    private static void SetupOrganizationCollectionAccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        params (Collection Collection, CollectionAccessDetails AccessDetails)[] collections)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdWithAccessAsync(organizationId)
            .Returns(collections
                .Select(c => new Tuple<Collection, CollectionAccessDetails>(c.Collection, c.AccessDetails))
                .ToList());
    }
}
