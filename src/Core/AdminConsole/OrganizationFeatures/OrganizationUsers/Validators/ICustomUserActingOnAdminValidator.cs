using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validators;

public interface ICustomUserActingOnAdminValidator
{
    /// <summary>
    /// Throws <see cref="Bit.Core.Exceptions.BadRequestException"/> when the current acting user
    /// has the Custom role in the target user's organization and the target user is an Admin.
    /// Custom users (even those granted the ManageUsers permission) are not permitted to modify
    /// Admins under the role hierarchy. The exception message is scoped to the supplied actionType.
    /// </summary>
    /// <param name="targetUser">The organization user that the acting user is attempting to modify.</param>
    /// <param name="actionType">The actionType being attempted; selects the error message.</param>
    Task EnforceAsync(OrganizationUser targetUser, OrganizationUserActionType actionType);

    /// <summary>
    /// Returns true when the current acting user has the Custom role in the target user's
    /// organization and the target user is an Admin. Custom users (even those granted the
    /// ManageUsers permission) are not permitted to modify Admins under the role hierarchy.
    /// </summary>
    /// <param name="targetUser">The organization user that the acting user is attempting to modify.</param>
    Task<bool> IsBlockedAsync(OrganizationUser targetUser);
}
