using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <summary>
/// Returned when an acting user attempts to act on (or assign) an organization role that outranks their own.
/// </summary>
public record CannotManageHigherRoleError()
    : BadRequestError("You cannot perform this action on a member with a higher organization role than your own.");

/// <summary>
/// Returned when the acting user has no authority to manage members for the action — for example a regular
/// User, or a Custom user without the required permission.
/// </summary>
public record MissingManagePermissionError()
    : BadRequestError("You do not have permission to manage organization members.");
