using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class CollectionRulesTests
{
    [Fact]
    public void CanUpdate_WithEditAnyCollectionPermission_Success()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { EditAnyCollection = true });

        var result = CollectionRules.OrganizationWide.CanUpdate(organization, Ability(allowAdminAccess: false));

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanUpdate_WhenAdminOrOwner_AllowAdminAccessTrue_Success(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.OrganizationWide.CanUpdate(organization, Ability(allowAdminAccess: true));

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanUpdate_WhenAdminOrOwner_AllowAdminAccessFalse_Failure(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.OrganizationWide.CanUpdate(organization, Ability(allowAdminAccess: false));

        Assert.False(result);
    }

    [Fact]
    public void CanUpdate_CustomUserWithManageUsersPermission_DoesNotGrantAccess()
    {
        // ManageUsers authorizes a change to user access only. It must not authorize an update to the
        // collection metadata.
        var organization = Organization(OrganizationUserType.Custom, new Permissions { ManageUsers = true });

        var result = CollectionRules.OrganizationWide.CanUpdate(organization, Ability(allowAdminAccess: true));

        Assert.False(result);
    }

    [Fact]
    public void CanUpdate_WhenMissingOrgAccess_Failure()
    {
        var result = CollectionRules.OrganizationWide.CanUpdate(null, Ability(allowAdminAccess: true));

        Assert.False(result);
    }

    [Fact]
    public void CanModifyUserAccess_WithEditAnyCollectionPermission_Success()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { EditAnyCollection = true });

        var result = CollectionRules.OrganizationWide.CanModifyUserAccess(organization, Ability(allowAdminAccess: false));

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyUserAccess_WithManageUsersPermission_AllowAdminAccessTrue_Success(OrganizationUserType type)
    {
        var organization = Organization(type, new Permissions { ManageUsers = true });

        var result = CollectionRules.OrganizationWide.CanModifyUserAccess(organization, Ability(allowAdminAccess: true));

        Assert.True(result);
    }

    [Fact]
    public void CanModifyUserAccess_CustomUserWithManageUsersPermission_AllowAdminAccessTrue_Success()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { ManageUsers = true });

        var result = CollectionRules.OrganizationWide.CanModifyUserAccess(organization, Ability(allowAdminAccess: true));

        Assert.True(result);
    }

    [Fact]
    public void CanModifyUserAccess_CustomUserWithManageUsersPermission_AllowAdminAccessFalse_Failure()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { ManageUsers = true });

        var result = CollectionRules.OrganizationWide.CanModifyUserAccess(organization, Ability(allowAdminAccess: false));

        Assert.False(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyUserAccess_WhenAdminOrOwner_AllowAdminAccessTrue_Success(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.OrganizationWide.CanModifyUserAccess(organization, Ability(allowAdminAccess: true));

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyUserAccess_WhenAdminOrOwner_AllowAdminAccessFalse_Failure(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.OrganizationWide.CanModifyUserAccess(organization, Ability(allowAdminAccess: false));

        Assert.False(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public void CanModifyUserAccess_WhenMissingPermissions_Failure(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.OrganizationWide.CanModifyUserAccess(organization, Ability(allowAdminAccess: true));

        Assert.False(result);
    }

    [Fact]
    public void CanModifyUserAccess_WhenMissingOrgAccess_Failure()
    {
        var result = CollectionRules.OrganizationWide.CanModifyUserAccess(null, Ability(allowAdminAccess: true));

        Assert.False(result);
    }

    [Fact]
    public void CanModifyGroupAccess_WithEditAnyCollectionPermission_Success()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { EditAnyCollection = true });

        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(organization, Ability(allowAdminAccess: false));

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyGroupAccess_WithManageGroupsPermission_AllowAdminAccessTrue_Success(OrganizationUserType type)
    {
        var organization = Organization(type, new Permissions { ManageGroups = true });

        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(organization, Ability(allowAdminAccess: true));

        Assert.True(result);
    }

    [Fact]
    public void CanModifyGroupAccess_CustomUserWithManageGroupsPermission_AllowAdminAccessTrue_Success()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { ManageGroups = true });

        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(organization, Ability(allowAdminAccess: true));

        Assert.True(result);
    }

    [Fact]
    public void CanModifyGroupAccess_CustomUserWithManageGroupsPermission_AllowAdminAccessFalse_Failure()
    {
        var organization = Organization(OrganizationUserType.Custom, new Permissions { ManageGroups = true });

        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(organization, Ability(allowAdminAccess: false));

        Assert.False(result);
    }

    [Fact]
    public void CanModifyGroupAccess_CustomUserWithManageUsersPermission_DoesNotGrantAccess()
    {
        // ManageUsers must not authorize a change to group access. ManageGroups authorizes that change.
        var organization = Organization(OrganizationUserType.Custom, new Permissions { ManageUsers = true });

        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(organization, Ability(allowAdminAccess: true));

        Assert.False(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyGroupAccess_WhenAdminOrOwner_AllowAdminAccessTrue_Success(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(organization, Ability(allowAdminAccess: true));

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanModifyGroupAccess_WhenAdminOrOwner_AllowAdminAccessFalse_Failure(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(organization, Ability(allowAdminAccess: false));

        Assert.False(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public void CanModifyGroupAccess_WhenMissingPermissions_Failure(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(organization, Ability(allowAdminAccess: true));

        Assert.False(result);
    }

    [Fact]
    public void CanModifyGroupAccess_WhenMissingOrgAccess_Failure()
    {
        var result = CollectionRules.OrganizationWide.CanModifyGroupAccess(null, Ability(allowAdminAccess: true));

        Assert.False(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public void CanManage_WhenCallerManagesCollection_Success(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.PerCollection.CanManage(organization, callerManagesCollection: true, isCollectionOrphaned: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanManage_WhenAdminOrOwnerAndCollectionOrphaned_Success(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.PerCollection.CanManage(organization, callerManagesCollection: false, isCollectionOrphaned: true);

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public void CanManage_WhenAdminOrOwnerAndCollectionNotOrphaned_Failure(OrganizationUserType type)
    {
        var organization = Organization(type);

        var result = CollectionRules.PerCollection.CanManage(organization, callerManagesCollection: false, isCollectionOrphaned: false);

        Assert.False(result);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public void CanManage_WhenNotAdminOrOwner_OrphanedDoesNotGrantAccess(OrganizationUserType type)
    {
        // Only Owners and Admins can manage orphaned collections. For any other member, an orphaned
        // collection does not authorize the operation.
        var organization = Organization(type);

        var result = CollectionRules.PerCollection.CanManage(organization, callerManagesCollection: false, isCollectionOrphaned: true);

        Assert.False(result);
    }

    [Fact]
    public void CanManage_WhenMissingOrgAccess_OrphanedDoesNotGrantAccess()
    {
        var result = CollectionRules.PerCollection.CanManage(null, callerManagesCollection: false, isCollectionOrphaned: true);

        Assert.False(result);
    }

    [Fact]
    public void IsOrphaned_WhenNoUserOrGroupManages_ReturnsTrue()
    {
        var accessDetails = AccessDetails(userManages: false, groupManages: false);

        var result = CollectionRules.PerCollection.IsOrphaned(accessDetails);

        Assert.True(result);
    }

    [Fact]
    public void IsOrphaned_WhenAUserManages_ReturnsFalse()
    {
        var accessDetails = AccessDetails(userManages: true, groupManages: false);

        var result = CollectionRules.PerCollection.IsOrphaned(accessDetails);

        Assert.False(result);
    }

    [Fact]
    public void IsOrphaned_WhenAGroupManages_ReturnsFalse()
    {
        var accessDetails = AccessDetails(userManages: false, groupManages: true);

        var result = CollectionRules.PerCollection.IsOrphaned(accessDetails);

        Assert.False(result);
    }

    private static CurrentContextOrganization Organization(OrganizationUserType type, Permissions permissions = null) =>
        new() { Type = type, Permissions = permissions ?? new Permissions() };

    private static OrganizationAbility Ability(bool allowAdminAccess) =>
        new() { AllowAdminAccessToAllCollectionItems = allowAdminAccess };

    private static CollectionAccessDetails AccessDetails(bool userManages, bool groupManages) =>
        new()
        {
            Users = [new CollectionAccessSelection { Manage = userManages }],
            Groups = [new CollectionAccessSelection { Manage = groupManages }],
        };
}
