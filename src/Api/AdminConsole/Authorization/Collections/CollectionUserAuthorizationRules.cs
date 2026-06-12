using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Authorization rules for modifying user access on collections.
/// </summary>
public static class CollectionUserAuthorizationRules
{
    public static bool CanModifyUserAccess(
        Collection collection,
        CurrentContextOrganization? organization,
        CollectionAccessContext collectionAccessContext)
    {
        if (organization is { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        if (collectionAccessContext.AllowAdminAccessToAllCollectionItems &&
            organization is { Permissions.ManageUsers: true })
        {
            return true;
        }

        if (collectionAccessContext.AllowAdminAccessToAllCollectionItems &&
            organization is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return true;
        }

        if (collectionAccessContext.CallerManagedCollectionIds.Contains(collection.Id))
        {
            return true;
        }

        if (organization is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } &&
            collectionAccessContext.OrphanedCollectionIds.Contains(collection.Id))
        {
            return true;
        }

        return collectionAccessContext.CallerIsProviderUser;
    }

    public static bool CanAddSelf(bool allowAdminAccessToAllCollectionItems) =>
        allowAdminAccessToAllCollectionItems;
}
