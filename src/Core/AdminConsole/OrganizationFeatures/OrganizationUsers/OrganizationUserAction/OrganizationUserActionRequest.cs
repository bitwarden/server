using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <summary>
/// Whether an acting user may manage organization members at all, independent of any target.
/// <see cref="OrganizationUserActionRequest"/> adds a target role for the full role-escalation check.
/// </summary>
/// <param name="ActingUserRole">The acting user's role, or <c>null</c> if not a confirmed member (they may still be authorized via <paramref name="IsProviderUserForOrg"/>).</param>
/// <param name="ActingUserPermissions">The acting user's custom permissions. Only consulted for Custom users.</param>
/// <param name="PermissionPicker">Picks the permission that authorizes a Custom user for this action (e.g. <c>p =&gt; p.ManageUsers</c>). Only consulted for Custom users.</param>
/// <param name="IsProviderUserForOrg">Whether the acting user is a provider user for the org, which grants Owner-level authority. Invoked last because it hits the database.</param>
public record OrganizationUserManageMembersRequest(
    OrganizationUserType? ActingUserRole,
    Permissions? ActingUserPermissions,
    Func<Permissions, bool> PermissionPicker,
    Func<Task<bool>> IsProviderUserForOrg);

/// <summary>
/// Extends <see cref="OrganizationUserManageMembersRequest"/> (see it for the acting-user fields) with the
/// target role, to decide whether the acting user may manage (or assign) that role without escalating privileges.
/// </summary>
/// <param name="TargetUserRole">The role being acted upon — an existing member's current role, or the new role being assigned.</param>
public record OrganizationUserActionRequest(
    OrganizationUserType? ActingUserRole,
    Permissions? ActingUserPermissions,
    Func<Permissions, bool> PermissionPicker,
    Func<Task<bool>> IsProviderUserForOrg,
    OrganizationUserType TargetUserRole)
    : OrganizationUserManageMembersRequest(ActingUserRole, ActingUserPermissions, PermissionPicker, IsProviderUserForOrg);
