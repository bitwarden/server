using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

public class CollectionGroupAuthorizationRulesTests
{
    [Fact]
    public void CanModifyGroupAccess_WithEditAnyCollectionPermission_ReturnsTrue()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization { Permissions = new Permissions { EditAnyCollection = true } };
        var ctx = EmptyContext();

        Assert.True(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner)]
    public void CanModifyGroupAccess_WithAdminOrOwnerAndAllowAdminAccess_ReturnsTrue(OrganizationUserType type)
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization { Type = type, Permissions = new Permissions() };
        var ctx = EmptyContext() with { AllowAdminAccessToAllCollectionItems = true };

        Assert.True(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner)]
    public void CanModifyGroupAccess_WithAdminOrOwnerAndNoAllowAdminAccess_ReturnsFalse(OrganizationUserType type)
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization { Type = type, Permissions = new Permissions() };
        var ctx = EmptyContext() with { AllowAdminAccessToAllCollectionItems = false };

        Assert.False(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyGroupAccess_WithCustomManageGroupsAndAllowAdminAccess_ReturnsTrue()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization
        {
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { ManageGroups = true }
        };
        var ctx = EmptyContext() with { AllowAdminAccessToAllCollectionItems = true };

        Assert.True(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyGroupAccess_WithCustomManageGroupsAndNoAllowAdminAccess_ReturnsFalse()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization
        {
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { ManageGroups = true }
        };
        var ctx = EmptyContext() with { AllowAdminAccessToAllCollectionItems = false };

        Assert.False(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyGroupAccess_WithCallerManagedCollection_ReturnsTrue()
    {
        var collectionId = Guid.NewGuid();
        var collection = new Collection { Id = collectionId };
        var org = new CurrentContextOrganization
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions()
        };
        var ctx = EmptyContext() with
        {
            CallerManagedCollectionIds = new HashSet<Guid> { collectionId }
        };

        Assert.True(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyGroupAccess_WithCallerNotManagingCollection_ReturnsFalse()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions()
        };
        var ctx = EmptyContext();

        Assert.False(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner)]
    public void CanModifyGroupAccess_WithOrphanedCollectionAndAdminRole_ReturnsTrue(OrganizationUserType type)
    {
        var collectionId = Guid.NewGuid();
        var collection = new Collection { Id = collectionId };
        var org = new CurrentContextOrganization { Type = type, Permissions = new Permissions() };
        var ctx = EmptyContext() with
        {
            OrphanedCollectionIds = new HashSet<Guid> { collectionId }
        };

        Assert.True(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyGroupAccess_WithOrphanedCollectionAndUserRole_ReturnsFalse()
    {
        var collectionId = Guid.NewGuid();
        var collection = new Collection { Id = collectionId };
        var org = new CurrentContextOrganization
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions()
        };
        var ctx = EmptyContext() with
        {
            OrphanedCollectionIds = new HashSet<Guid> { collectionId }
        };

        Assert.False(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyGroupAccess_WithProviderUser_ReturnsTrue()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var ctx = EmptyContext() with { CallerIsProviderUser = true };

        Assert.True(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, null, ctx));
    }

    [Fact]
    public void CanModifyGroupAccess_WithNoOrgAccess_ReturnsFalse()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var ctx = EmptyContext();

        Assert.False(CollectionGroupAuthorizationRules.CanModifyGroupAccess(collection, null, ctx));
    }

    private static CollectionAccessAuthorizationContext EmptyContext() =>
        new(
            AllowAdminAccessToAllCollectionItems: false,
            CallerIsProviderUser: false,
            CallerManagedCollectionIds: [],
            OrphanedCollectionIds: []);
}
