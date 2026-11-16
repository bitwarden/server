using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Business rules for collection authorization. They do not read the database;
/// <see cref="CollectionAuthorizationService"/> loads the data they need.
/// </summary>
public static class CollectionRules
{
    /// <summary>
    /// Permissions that apply to one collection. A false result does not deny the operation;
    /// <see cref="OrganizationRole"/> can still authorize it.
    /// </summary>
    public static class CollectionAssignment
    {
        /// <summary>
        /// Returns true if the caller has Manage on the collection, or is an Owner or Admin and the collection is
        /// orphaned.
        /// </summary>
        public static bool CanManage(CurrentContextOrganization? organizationClaims, CollectionAdminDetails details) =>
            details.Manage ||
            (details.Unmanaged && CanManageOrphanedCollections(organizationClaims));

        private static bool CanManageOrphanedCollections(CurrentContextOrganization? organizationClaims) =>
            organizationClaims is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin };
    }

    /// <summary>
    /// Permissions that apply to every collection in the organization. A false result does not deny the operation;
    /// <see cref="CollectionAssignment"/> can still authorize it.
    /// </summary>
    /// <remarks>These rules cover the collection itself, not its items.</remarks>
    public static class OrganizationRole
    {
        /// <summary>
        /// Returns true if the caller can update the name and external ID of every collection.
        /// </summary>
        public static bool CanUpdate(CurrentContextOrganization? organizationClaims, OrganizationAbility? organizationAbility) =>
            organizationClaims is { Permissions.EditAnyCollection: true } ||
            (AllowsAdminAccessToAllCollectionItems(organizationAbility) &&
             organizationClaims is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin });

        /// <summary>
        /// Returns true if the caller can modify the user access of every collection.
        /// </summary>
        public static bool CanModifyUserAccess(CurrentContextOrganization? organizationClaims, OrganizationAbility? organizationAbility) =>
            CanUpdate(organizationClaims, organizationAbility) ||
            (AllowsAdminAccessToAllCollectionItems(organizationAbility) &&
             organizationClaims is { Permissions.ManageUsers: true });

        /// <summary>
        /// Returns true if the caller can modify the group access of every collection.
        /// </summary>
        public static bool CanModifyGroupAccess(CurrentContextOrganization? organizationClaims, OrganizationAbility? organizationAbility) =>
            CanUpdate(organizationClaims, organizationAbility) ||
            (AllowsAdminAccessToAllCollectionItems(organizationAbility) &&
             organizationClaims is { Permissions.ManageGroups: true });

        private static bool AllowsAdminAccessToAllCollectionItems(OrganizationAbility? organizationAbility) =>
            organizationAbility is { AllowAdminAccessToAllCollectionItems: true };
    }

    /// <summary>
    /// Permissions of a user of the provider that manages the organization. They apply to every collection.
    /// </summary>
    public static class ManagingProvider
    {
        /// <summary>
        /// Returns true if users of <paramref name="provider"/> can manage the collections of its organizations.
        /// </summary>
        public static bool CanManageCollections(Provider? provider) =>
            provider is { Enabled: true, Type: ProviderType.Msp or ProviderType.BusinessUnit };
    }
}
