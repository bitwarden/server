#nullable enable
using Bit.Core.Context;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Decides whether a user can change another user's access to a collection.
/// </summary>
public static class CollectionUserAuthorizationRules
{
    /// <summary>
    /// Returns true if the acting user can add, change, or remove another user's access to this collection.
    /// <paramref name="callerManagesCollection"/> means the acting user has <c>Manage</c> on this collection,
    /// either directly or through a group.
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

        if (allowAdminAccessToAllCollectionItems && organization?.HasPermission(p => p.ManageUsers) == true)
        {
            return true;
        }

        if (allowAdminAccessToAllCollectionItems && organization is { IsAdminOrOwner: true })
        {
            return true;
        }

        if (callerManagesCollection)
        {
            return true;
        }

        // Owners and Admins can still manage a collection with no manager, even if
        // AllowAdminAccessToAllCollectionItems is off. (EditAnyCollection callers already returned above.)
        if (organization is not { IsAdminOrOwner: true })
        {
            return false;
        }

        var isOrphaned = !accessDetails.Users.Any(u => u.Manage) && !accessDetails.Groups.Any(g => g.Manage);
        return isOrphaned;
    }
}
