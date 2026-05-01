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
        CurrentContextOrganization? org,
        CollectionAccessAuthorizationContext ctx)
    {
        if (org is { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        if (ctx.AllowAdminAccessToAllCollectionItems &&
            org is { Permissions.ManageUsers: true })
        {
            return true;
        }

        if (ctx.AllowAdminAccessToAllCollectionItems &&
            org is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return true;
        }

        if (ctx.CallerManagedCollectionIds.Contains(collection.Id))
        {
            return true;
        }

        if (org is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } &&
            ctx.OrphanedCollectionIds.Contains(collection.Id))
        {
            return true;
        }

        return ctx.CallerIsProviderUser;
    }

    public static bool CanAddSelf(bool allowAdminAccessToAllCollectionItems) =>
        allowAdminAccessToAllCollectionItems;
}
