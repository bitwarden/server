using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <summary>
/// Returned when the acting user lacks the authority to act on the target member's role.
/// </summary>
public record CannotManageTargetUser()
    : BadRequestError("You do not have permission to manage this user.");
