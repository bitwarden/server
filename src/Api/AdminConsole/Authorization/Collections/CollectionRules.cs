using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Business rules for collection authorization. These rules do not read the database.
/// <see cref="OrganizationRole"/> holds the permissions that apply to every collection in the organization.
/// <see cref="CollectionAssignment"/> holds the permissions that apply to one collection.
/// <see cref="CollectionAuthorizationService"/> reads the data these rules need and calls both sets.
/// </summary>
public static class CollectionRules
{
    /// <summary>
    /// Rules that authorize an operation on one collection, without an organization-wide permission.
    /// A false result does not deny the operation. The caller can still be authorized by
    /// <see cref="OrganizationRole"/>.
    /// </summary>
    public static class CollectionAssignment
    {
        /// <summary>
        /// Returns true if the caller can manage one collection without an organization-wide permission.
        /// This is true if the caller has Manage access to the collection, or if the caller is an Owner or
        /// Admin and the collection is orphaned.
        /// </summary>
        /// <remarks>Being assigned to the collection is not enough. The caller needs Manage.</remarks>
        public static bool CanManage(CurrentContextOrganization? organizationClaims, CollectionAdminDetails details) =>
            details.Manage ||
            (details.Unmanaged && CanManageOrphanedCollections(organizationClaims));

        /// <summary>
        /// Returns true if the caller can manage orphaned collections.
        /// </summary>
        private static bool CanManageOrphanedCollections(CurrentContextOrganization? organizationClaims) =>
            organizationClaims is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin };
    }

    /// <summary>
    /// Permissions that authorize an operation on every collection in the organization, including the collections
    /// that the caller is not assigned to. A false result does not deny the operation. The caller can still be
    /// authorized for one collection by <see cref="CollectionAssignment.CanManage"/>.
    /// </summary>
    public static class OrganizationRole
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
