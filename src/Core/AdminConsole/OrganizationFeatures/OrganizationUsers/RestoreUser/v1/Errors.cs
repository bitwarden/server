using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;

public record UserCannotBeRestoredNotCompliantWithPolicies(string Email)
    : BadRequestError($"{Email} is not compliant with the single organization membership and two-step login policy.");

public record UserCannotBeRestoredFreeOrgAdminLimit()
    : BadRequestError("User is an owner / admin of another free organization vault. Please have them upgrade to a paid plan to restore their account.");

public record CannotRestoreYourself() : BadRequestError("You cannot restore yourself.");
public record OnlyOwnersCanRestoreOwners() : BadRequestError("Only owners can restore other owners.");
public record CustomUsersCannotRestoreAdmins() : BadRequestError("Custom users can not restore admins.");
public record AlreadyActive() : BadRequestError("Already active.");
public record UsersInvalid() : BadRequestError("Users invalid.");
public record UserNotCompliantWithTwoFactorPolicy(string Email) : BadRequestError($"{Email} is not compliant with the two-step login policy.");
