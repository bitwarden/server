using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

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
    /// This should be used in combination with an <c>AuthorizeAttribute</c> for the standard RBAC check on the
    /// controller endpoint.
    /// This will allow Owners to manage provider users, which is suitable for most organization-level concerns.
    /// <remarks>
    /// If your operation affects the provider user as a provider user (e.g. password reset, where account takeover
    /// would enable escalation) you may not want to allow this.
    /// </remarks>
    /// </summary>
    /// <param name="actingUserId">The acting user's id, used to resolve provider authority.</param>
    /// <param name="actingUser">The acting user's membership, or <c>null</c> if not a confirmed member.</param>
    /// <param name="targetUser">The member being managed.</param>
    /// <returns><c>null</c> when allowed, otherwise a <see cref="CannotManageTargetUser"/>.</returns>
    Task<Error?> CanManage(Guid actingUserId, OrganizationUser? actingUser, OrganizationUser targetUser);
}
