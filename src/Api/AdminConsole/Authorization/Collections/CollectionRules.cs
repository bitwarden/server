#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Decides whether a user can update a collection itself, or change another user's or a group's access to it.
/// </summary>
public static class CollectionRules
{
    /// <summary>
    /// Returns true if the acting user can update the collection's own metadata (name, externalId).
    /// </summary>
    public static bool CanUpdate(
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization? organization,
        bool allowAdminAccessToAllCollectionItems,
        bool callerManagesCollection)
    {
        if (organization is { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        if (allowAdminAccessToAllCollectionItems && organization is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return true;
        }

        if (callerManagesCollection)
        {
            return true;
        }

        // Owners/Admins can still manage an orphaned collection even when AllowAdminAccessToAllCollectionItems is off.
        if (organization is not { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return false;
        }

        var isOrphaned = !accessDetails.Users.Any(u => u.Manage) && !accessDetails.Groups.Any(g => g.Manage);
        return isOrphaned;
    }

    /// <summary>
    /// Returns true if the acting user can add, change, or remove another user's access to this collection.
    /// </summary>
    public static bool CanModifyUserAccess(
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization? organization,
        bool allowAdminAccessToAllCollectionItems,
        bool callerManagesCollection)
    {
        if (organization is { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        if (allowAdminAccessToAllCollectionItems && organization is { Permissions.ManageUsers: true })
        {
            return true;
        }

        if (allowAdminAccessToAllCollectionItems && organization is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return true;
        }

        if (callerManagesCollection)
        {
            return true;
        }

        // Owners/Admins can still manage an orphaned collection even when AllowAdminAccessToAllCollectionItems is off.
        if (organization is not { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return false;
        }

        var isOrphaned = !accessDetails.Users.Any(u => u.Manage) && !accessDetails.Groups.Any(g => g.Manage);
        return isOrphaned;
    }

    /// <summary>
    /// Returns true if the acting user can add, change, or remove a group's access to this collection.
    /// </summary>
    public static bool CanModifyGroupAccess(
        CollectionAccessDetails accessDetails,
        CurrentContextOrganization? organization,
        bool allowAdminAccessToAllCollectionItems,
        bool callerManagesCollection)
    {
        if (organization is { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        if (allowAdminAccessToAllCollectionItems && organization is { Permissions.ManageGroups: true })
        {
            return true;
        }

        if (allowAdminAccessToAllCollectionItems && organization is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return true;
        }

        if (callerManagesCollection)
        {
            return true;
        }

        // Owners/Admins can still manage an orphaned collection even when AllowAdminAccessToAllCollectionItems is off.
        if (organization is not { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return false;
        }

        var isOrphaned = !accessDetails.Users.Any(u => u.Manage) && !accessDetails.Groups.Any(g => g.Manage);
        return isOrphaned;
    }
}
