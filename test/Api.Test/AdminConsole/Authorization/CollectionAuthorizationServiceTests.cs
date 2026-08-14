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
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenCollectionBelongsToDifferentOrganization_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        Guid organizationId)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collection.Id);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenMissingUserId_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WithEditAnyCollectionPermission_Success(
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

        Assert.True(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenMissingPermissions_NoSuccess(
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

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenProviderUser_Success(
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

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WithManageUsersPermission_WhenAllowAdminAccessTrue_NoSuccess(
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

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenCallerManagesCollection_Success(
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

        Assert.True(result);
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

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeUpdateAsync_WhenOwnerAndCollectionNotOrphaned_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId,
        CollectionAccessSelection collectionManager)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();
        collectionManager.Manage = true;

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [collectionManager], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WhenCollectionNotFound_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, collectionId);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WithEditAnyCollectionPermission_Success(
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

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WhenProviderUser_Success(
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

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WithManageUsersPermission_WhenAllowAdminAccessTrue_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        // Unlike updating the collection's own metadata, ManageUsers + AllowAdminAccessToAllCollectionItems
        // is a real bypass for modifying user access.
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WithManageUsersPermission_WhenAllowAdminAccessFalse_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
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

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyUserAccessAsync_WhenOwnerAndCollectionNotOrphaned_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId,
        CollectionAccessSelection collectionManager)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();
        collectionManager.Manage = true;

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [collectionManager], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WhenCollectionNotFound_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, collectionId);

        Assert.False(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WithEditAnyCollectionPermission_Success(
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

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive().GetManyByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WhenProviderUser_Success(
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

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WithManageGroupsPermission_WhenAllowAdminAccessTrue_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WithManageGroupsPermission_WhenAllowAdminAccessFalse_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, accessDetails));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(collection.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
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

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(collection.OrganizationId, collection.Id);

        Assert.True(result);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task AuthorizeModifyGroupAccessAsync_WhenOwnerAndCollectionNotOrphaned_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Collection collection,
        CurrentContextOrganization organization,
        Guid userId,
        CollectionAccessSelection collectionManager)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();
        collectionManager.Manage = true;

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [collectionManager] }));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(collection.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(collection.OrganizationId, collection.Id);

        Assert.False(result);
    }
}
