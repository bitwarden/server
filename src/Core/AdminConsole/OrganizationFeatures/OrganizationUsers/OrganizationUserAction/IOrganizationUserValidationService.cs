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
    /// Callers must separately confirm the acting user can manage members at all.
    /// For a role change, call this for both the current and the new role.
    /// </summary>
    /// <param name="actingUser">The acting user's membership, or <c>null</c> if not a confirmed member.</param>
    /// <param name="targetUser">The member being managed.</param>
    /// <param name="actingUserIsProvider">Whether the acting user is a provider user, which grants Owner-level authority.</param>
    /// <returns><c>null</c> when allowed, otherwise a <see cref="CannotManageTargetUser"/>.</returns>
    Error? CanManage(OrganizationUser? actingUser, OrganizationUser targetUser, bool actingUserIsProvider);
}
