using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Authorization rules for modifying group access on collections.
/// </summary>
public static class CollectionGroupAuthorizationRules
{
    public static bool CanModifyGroupAccess(
        Collection collection,
        CurrentContextOrganization? org,
        CollectionAccessAuthorizationContext ctx)
    {
        if (ctx.AllowAdminAccessToAllCollectionItems &&
            org is { Permissions.ManageGroups: true })
        {
            return true;
        }

        if (org is { Permissions.EditAnyCollection: true })
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
}
