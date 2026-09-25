#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Reusable, pure permission checks for Collections that other authorization requirements/handlers can depend on
/// without needing to invoke <see cref="BulkCollectionAuthorizationHandler"/> directly.
/// </summary>
public static class CollectionPermissions
{
    /// <summary>
    /// Returns true if the user is allowed to create a new collection in the organization.
    /// This does not account for Provider users - callers must check that separately (it requires a database call).
    /// </summary>
    public static bool CanCreate(CurrentContextOrganization? organizationClaims, OrganizationAbility? organizationAbility)
    {
        // Owners, Admins, and users with CreateNewCollections permission can always create collections
        if (organizationClaims is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.CreateNewCollections: true })
        {
            return true;
        }

        // If the limit collection creation setting is disabled, allow any member to create collections
        return organizationClaims is not null && organizationAbility is not { LimitCollectionCreation: true };
    }
}
