using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <summary>
/// Reusable organization-user authorization rules shared by command-specific validators.
/// </summary>
public interface IManageOrganizationUserValidationService
{
    /// <summary>
    /// Checks whether the acting user can manage the target user without escalating privileges:
    /// <list type="bullet">
    /// <item>Owners and provider users can manage anyone.</item>
    /// <item>Admins can manage anyone except Owners.</item>
    /// <item>Custom users with ManageUsers can manage Users and other Custom users.</item>
    /// <item>Everyone else has no authority.</item>
    /// </list>
    /// Pair with an <c>AuthorizeAttribute</c> for the standard RBAC check on the endpoint.
    /// </summary>
    /// <remarks>
    /// Owners are allowed to manage provider users. If your operation affects the provider user as a provider user
    /// (e.g. password reset, where account takeover would enable escalation) you may not want to allow this.
    /// </remarks>
    /// <param name="actingUserId">The acting user's id, used to resolve provider authority.</param>
    /// <param name="actingUser">The acting user's role, or <c>null</c> if not a confirmed member.</param>
    /// <param name="targetUser">The member being managed.</param>
    /// <returns><c>null</c> when allowed, otherwise a <see cref="CannotManageTargetUser"/>.</returns>
    Task<Error?> CanManageAsync(Guid actingUserId, IOrganizationUserRole? actingUser, IOrganizationUserRole targetUser);

    /// <summary>
    /// Checks whether the acting user can change the target member's role without escalating privileges. The acting
    /// user must be able to manage both the target's current and requested role, and a Custom user may only grant
    /// custom permissions they hold themselves.
    /// </summary>
    /// <param name="actingUserId">The acting user's id, used to resolve provider authority.</param>
    /// <param name="actingUser">The acting user's role.</param>
    /// <param name="targetUser">The member being managed, with their current role.</param>
    /// <param name="targetNewUserType">The role the target member is being changed to.</param>
    /// <param name="targetNewPermissions">The custom permissions being granted, if any.</param>
    /// <returns><c>null</c> when allowed, otherwise the error describing the denial.</returns>
    Task<Error?> CanManageRoleChangeAsync(Guid actingUserId, IOrganizationUserRole actingUser, IOrganizationUserRole targetUser,
        OrganizationUserType targetNewUserType, Permissions? targetNewPermissions);
}
