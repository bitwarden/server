using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class CollectionUserAuthorizationRulesTests
{
    [Fact]
    public void CanModifyUserAccess_WithEditAnyCollectionPermission_ReturnsTrue()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization { Permissions = new Permissions { EditAnyCollection = true } };
        var ctx = EmptyContext();

        Assert.True(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner)]
    public void CanModifyUserAccess_WithAdminOrOwnerAndAllowAdminAccess_ReturnsTrue(OrganizationUserType type)
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization { Type = type, Permissions = new Permissions() };
        var ctx = EmptyContext() with { AllowAdminAccessToAllCollectionItems = true };

        Assert.True(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner)]
    public void CanModifyUserAccess_WithAdminOrOwnerAndNoAllowAdminAccess_ReturnsFalse(OrganizationUserType type)
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization { Type = type, Permissions = new Permissions() };
        var ctx = EmptyContext() with { AllowAdminAccessToAllCollectionItems = false };

        Assert.False(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyUserAccess_WithCustomManageUsersAndAllowAdminAccess_ReturnsTrue()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization
        {
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { ManageUsers = true }
        };
        var ctx = EmptyContext() with { AllowAdminAccessToAllCollectionItems = true };

        Assert.True(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyUserAccess_WithCustomManageUsersAndNoAllowAdminAccess_ReturnsFalse()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization
        {
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { ManageUsers = true }
        };
        var ctx = EmptyContext() with { AllowAdminAccessToAllCollectionItems = false };

        Assert.False(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyUserAccess_WithCallerManagedCollection_ReturnsTrue()
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

        Assert.True(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyUserAccess_WithCallerNotManagingCollection_ReturnsFalse()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var org = new CurrentContextOrganization
        {
            Type = OrganizationUserType.User,
            Permissions = new Permissions()
        };
        var ctx = EmptyContext();

        Assert.False(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Theory]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner)]
    public void CanModifyUserAccess_WithOrphanedCollectionAndAdminRole_ReturnsTrue(OrganizationUserType type)
    {
        var collectionId = Guid.NewGuid();
        var collection = new Collection { Id = collectionId };
        var org = new CurrentContextOrganization { Type = type, Permissions = new Permissions() };
        var ctx = EmptyContext() with
        {
            OrphanedCollectionIds = new HashSet<Guid> { collectionId }
        };

        Assert.True(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyUserAccess_WithOrphanedCollectionAndUserRole_ReturnsFalse()
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

        Assert.False(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, org, ctx));
    }

    [Fact]
    public void CanModifyUserAccess_WithProviderUser_ReturnsTrue()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var ctx = EmptyContext() with { CallerIsProviderUser = true };

        Assert.True(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, null, ctx));
    }

    [Fact]
    public void CanModifyUserAccess_WithNoOrgAccess_ReturnsFalse()
    {
        var collection = new Collection { Id = Guid.NewGuid() };
        var ctx = EmptyContext();

        Assert.False(CollectionUserAuthorizationRules.CanModifyUserAccess(collection, null, ctx));
    }

    [Fact]
    public void CanAddSelf_WhenAllowAdminAccess_ReturnsTrue()
    {
        Assert.True(CollectionUserAuthorizationRules.CanAddSelf(allowAdminAccessToAllCollectionItems: true));
    }

    [Fact]
    public void CanAddSelf_WhenNoAllowAdminAccess_ReturnsFalse()
    {
        Assert.False(CollectionUserAuthorizationRules.CanAddSelf(allowAdminAccessToAllCollectionItems: false));
    }

    private static CollectionAccessAuthorizationContext EmptyContext() =>
        new(
            AllowAdminAccessToAllCollectionItems: false,
            CallerIsProviderUser: false,
            CallerManagedCollectionIds: [],
            OrphanedCollectionIds: []);
}
