using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class CollectionGroupAuthorizationRulesTests
{
    [Fact]
    public void CanModifyGroupAccess_WithEditAnyCollectionPermission_Success()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { EditAnyCollection = true });

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: true), organization,
            allowAdminAccessToAllCollectionItems: false, callerManagesCollection: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyGroupAccess_WithManageUsersPermission_AllowAdminAccessTrue_Success(OrganizationUserType type)
    {
        var organization = Organization(type, new Permissions { ManageUsers = true });

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: true), organization,
            allowAdminAccessToAllCollectionItems: true, callerManagesCollection: false);

        Assert.True(result);
    }

    [Fact]
    public void CanModifyGroupAccess_CustomUserWithManageUsersPermission_AllowAdminAccessFalse_Failure()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { ManageUsers = true });

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: true), organization,
            allowAdminAccessToAllCollectionItems: false, callerManagesCollection: false);

        Assert.False(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyGroupAccess_WhenAdminOrOwner_AllowAdminAccessTrue_Success(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: true), organization,
            allowAdminAccessToAllCollectionItems: true, callerManagesCollection: false);

        Assert.True(result);
    }

    [Fact]
    public void CanModifyGroupAccess_WhenCallerManagesCollection_Success()
    {
        var organization = Organization(OrganizationUserType.User);

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: true), organization,
            allowAdminAccessToAllCollectionItems: false, callerManagesCollection: true);

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyGroupAccess_WhenAdminOrOwner_AllowAdminAccessFalse_OrphanedCollection_Success(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: false), organization,
            allowAdminAccessToAllCollectionItems: false, callerManagesCollection: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyGroupAccess_WhenAdminOrOwner_AllowAdminAccessFalse_NotOrphaned_Failure(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: true), organization,
            allowAdminAccessToAllCollectionItems: false, callerManagesCollection: false);

        Assert.False(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public void CanModifyGroupAccess_WhenMissingPermissions_Failure(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: true), organization,
            allowAdminAccessToAllCollectionItems: true, callerManagesCollection: false);

        Assert.False(result);
    }

    [Fact]
    public void CanModifyGroupAccess_WhenMissingOrgAccess_Failure()
    {
        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            AccessDetails(anyoneManages: true), organization: null,
            allowAdminAccessToAllCollectionItems: true, callerManagesCollection: false);

        Assert.False(result);
    }

    [Fact]
    public void CanModifyGroupAccess_OrphanedViaGroupsOnly_WhenAdminOrOwner_AllowAdminAccessFalse_Failure()
    {
        // A collection with only a managing group (no managing users) is not orphaned. Admins/owners must
        // not gain implicit access.
        var accessDetails = new CollectionAccessDetails
        {
            Users = Array.Empty<CollectionAccessSelection>(),
            Groups = new[] { new CollectionAccessSelection { Id = Guid.NewGuid(), Manage = true } }
        };

        var result = CollectionGroupAuthorizationRules.CanModifyGroupAccess(
            accessDetails, Organization(OrganizationUserType.Admin),
            allowAdminAccessToAllCollectionItems: false, callerManagesCollection: false);

        Assert.False(result);
    }

    private static CollectionAccessDetails AccessDetails(bool anyoneManages) => new()
    {
        Users = anyoneManages
            ? new[] { new CollectionAccessSelection { Id = Guid.NewGuid(), Manage = true } }
            : Array.Empty<CollectionAccessSelection>(),
        Groups = Array.Empty<CollectionAccessSelection>()
    };

    private static CurrentContextOrganization Organization(OrganizationUserType type, Permissions permissions = null) =>
        new() { Type = type, Permissions = permissions ?? new Permissions() };
}
