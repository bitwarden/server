using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Business rules for collection authorization. These rules do not read the database.
/// <see cref="OrganizationWide"/> holds the permissions that apply to every collection in the organization.
/// <see cref="PerCollection"/> holds the permissions that apply to one collection.
/// <see cref="CollectionAuthorizationService"/> reads the data that these rules need, calls both sets of rules,
/// and applies the provider user bypass.
/// </summary>
public static class CollectionRules
{
    /// <summary>
    /// Rules that authorize an operation on one collection, without an organization-wide permission.
    /// A false result does not deny the operation. The caller can still be authorized by
    /// <see cref="OrganizationWide"/>.
    /// </summary>
    public static class PerCollection
    {
        /// <summary>
        /// Returns true if the caller can manage one collection without an organization-wide permission.
        /// This is true if the caller is assigned to manage the collection, or if the caller is an Owner or
        /// Admin and the collection is orphaned.
        /// </summary>
        public static bool CanManage(
            CurrentContextOrganization? organizationClaims,
            bool callerManagesCollection,
            bool isCollectionOrphaned) =>
            callerManagesCollection ||
            (isCollectionOrphaned && CanManageOrphanedCollections(organizationClaims));

        /// <summary>
        /// Returns true if the caller can manage orphaned collections. Callers can check this first and skip
        /// the lookup of orphaned collections when it returns false.
        /// </summary>
        public static bool CanManageOrphanedCollections(CurrentContextOrganization? organizationClaims) =>
            organizationClaims is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin };

        /// <summary>
        /// Returns true if the collection has no user or group set to manage it.
        /// </summary>
        public static bool IsOrphaned(CollectionAccessDetails accessDetails) =>
            !accessDetails.Users.Any(user => user.Manage) && !accessDetails.Groups.Any(group => group.Manage);
    }

    /// <summary>
    /// Permissions that authorize an operation on every collection in the organization, including the collections
    /// that the caller is not assigned to. A false result does not deny the operation. The caller can still be
    /// authorized for one collection by <see cref="PerCollection.CanManage"/>.
    /// </summary>
    public static class OrganizationWide
    {
        /// <summary>
        /// Returns true if the caller can update the metadata (name, externalId) of every collection in the
        /// organization.
        /// </summary>
        public static bool CanUpdate(CurrentContextOrganization? organizationClaims, OrganizationAbility? organizationAbility) =>
            organizationClaims is { Permissions.EditAnyCollection: true } ||
            (AllowsAdminAccessToAllCollectionItems(organizationAbility) &&
             organizationClaims is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin });

        /// <summary>
        /// Returns true if the caller can add, change, or remove the user access of every collection in the
        /// organization.
        /// </summary>
        public static bool CanModifyUserAccess(CurrentContextOrganization? organizationClaims, OrganizationAbility? organizationAbility) =>
            CanUpdate(organizationClaims, organizationAbility) ||
            (AllowsAdminAccessToAllCollectionItems(organizationAbility) &&
             organizationClaims is { Permissions.ManageUsers: true });

        /// <summary>
        /// Returns true if the caller can add, change, or remove the group access of every collection in the
        /// organization.
        /// </summary>
        public static bool CanModifyGroupAccess(CurrentContextOrganization? organizationClaims, OrganizationAbility? organizationAbility) =>
            CanUpdate(organizationClaims, organizationAbility) ||
            (AllowsAdminAccessToAllCollectionItems(organizationAbility) &&
             organizationClaims is { Permissions.ManageGroups: true });

        private static bool AllowsAdminAccessToAllCollectionItems(OrganizationAbility? organizationAbility) =>
            organizationAbility is { AllowAdminAccessToAllCollectionItems: true };
    }
}
