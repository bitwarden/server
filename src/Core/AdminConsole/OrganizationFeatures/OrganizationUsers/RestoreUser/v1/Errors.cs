using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;

public record UserCannotBeRestoredNotCompliantWithPolicies(string Email)
    : BadRequestError($"{Email} is not compliant with the single organization membership and two-step login policy.");

public record UserCannotBeRestoredFreeOrgAdminLimit()
    : BadRequestError("User is an owner / admin of another free organization vault. Please have them upgrade to a paid plan to restore their account.");
public record CannotRestoreYourselfError() : BadRequestError("You cannot restore yourself.");
public record OnlyOwnersCanRestoreOwnersError() : BadRequestError("Only owners can restore other owners.");
public record CustomUsersCannotRestoreAdminsError() : BadRequestError("Custom users can not restore admins.");
public record AlreadyActiveError() : BadRequestError("Already active.");
public record UsersInvalidError() : BadRequestError("Users invalid.");
public record UserNotCompliantWithTwoFactorPolicyError(string Email) : BadRequestError($"{Email} is not compliant with the two-step login policy");
public record UserCannotBeRestoredAutoConfirmMemberOfAnotherOrg(string Email) : BadRequestError($"{Email} cannot be restored until they leave all other organization vaults.");
public record UserCannotBeRestoredAutoConfirmForbiddenByOtherOrg(string Email) : BadRequestError($"{Email} cannot be restored because they are a member of another organization which forbids it.");
