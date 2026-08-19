using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <summary>
/// The custom permission that grants a Custom user authority to manage Users and other Custom users in
/// <see cref="IOrganizationUserValidationService.CanManageAsync(Guid, IOrganizationUserRole?, IOrganizationUserRole, CustomUserManagePermission)"/>.
/// A closed set keeps the mapping to <see cref="Permissions"/> inside the validation service, instead of letting
/// callers hand it an arbitrary predicate.
/// </summary>
public enum CustomUserManagePermission
{
    /// <summary>The default gate: managing regular Users and other Custom users.</summary>
    ManageUsers,

    /// <summary>Gates flows that are scoped to account recovery instead of general user management.</summary>
    ManageResetPassword
}

/// <summary>
/// Reusable organization-user authorization rules shared by command-specific validators.
/// </summary>
public interface IOrganizationUserValidationService
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
    /// <param name="customPermissionGate">
    /// The custom permission that grants a Custom user authority. Defaults to <see cref="CustomUserManagePermission.ManageUsers"/>;
    /// pass <see cref="CustomUserManagePermission.ManageResetPassword"/> for flows gated by that permission instead.
    /// </param>
    /// <returns><c>null</c> when allowed, otherwise the error explaining why.</returns>
    Task<Error?> CanManageAsync(Guid actingUserId, IOrganizationUserRole? actingUser, IOrganizationUserRole targetUser,
        CustomUserManagePermission customPermissionGate = CustomUserManagePermission.ManageUsers);

    /// <summary>
    /// Bulk form of <see cref="CanManageAsync(Guid, IOrganizationUserRole?, IOrganizationUserRole, CustomUserManagePermission)"/>
    /// for checking many targets in the same organization at once. Provider authority is resolved once for the whole
    /// batch instead of once per target.
    /// </summary>
    /// <param name="actingUserId">The acting user's id, used to resolve provider authority.</param>
    /// <param name="actingUser">The acting user's role, or <c>null</c> if not a confirmed member.</param>
    /// <param name="organizationId">The organization all target users belong to.</param>
    /// <param name="targetUsersById">The members being managed, keyed by their <c>OrganizationUserId</c>.</param>
    /// <param name="customPermissionGate">See <see cref="CanManageAsync(Guid, IOrganizationUserRole?, IOrganizationUserRole, CustomUserManagePermission)"/>.</param>
    /// <returns>A <see cref="ManageAuthorizationResult"/> per target, keyed by <c>OrganizationUserId</c>.</returns>
    Task<IReadOnlyDictionary<Guid, ManageAuthorizationResult>> CanManageAsync(Guid actingUserId, IOrganizationUserRole? actingUser,
        Guid organizationId, IReadOnlyDictionary<Guid, IOrganizationUserRole> targetUsersById,
        CustomUserManagePermission customPermissionGate = CustomUserManagePermission.ManageUsers);

    /// <summary>
    /// Checks whether the acting user can change the target member's role without escalating privileges. The acting
    /// user must be able to manage both the target's current and requested role, and a Custom user may only grant
    /// custom permissions they hold themselves.
    /// </summary>
    /// <param name="actingUserId">
    /// The acting user's id, used to resolve provider authority. May be <c>null</c> when the caller authenticates
    /// via an organization API key (no individual user identity), in which case provider-user authority is skipped
    /// and only role-based authority is evaluated.
    /// </param>
    /// <param name="actingUser">The acting user's role, or <c>null</c> if not a confirmed member.</param>
    /// <param name="targetUser">The member being managed, with their current role.</param>
    /// <param name="newTargetUser">The updated member being managed (desired role and permissions).</param>
    /// <returns><c>null</c> when allowed, otherwise the error describing the denial.</returns>
    Task<Error?> CanManageRoleChangeAsync(Guid? actingUserId, IOrganizationUserRole? actingUser, IOrganizationUserRole targetUser,
        IOrganizationUserRole newTargetUser);

    /// <summary>
    /// On a Free plan, a user may only be an Admin or Owner of a single organization. Checks whether giving the user
    /// the requested role would exceed that limit, accounting for the organization they are already a member of.
    /// </summary>
    /// <param name="userId">The member's user id, or <c>null</c> for an invite that isn't linked to a user yet.</param>
    /// <param name="planType">The organization's plan.</param>
    /// <param name="currentUserType">The member's current role (already counted toward the limit if privileged).</param>
    /// <param name="newUserType">The role being requested.</param>
    /// <returns><c>null</c> when allowed, otherwise a <see cref="CannotBeAdminOfMultipleFreeOrganizations"/>.</returns>
    Task<Error?> ValidateFreeOrgAdminLimitAsync(Guid? userId, PlanType planType, OrganizationUserType currentUserType,
        OrganizationUserType newUserType);
}
