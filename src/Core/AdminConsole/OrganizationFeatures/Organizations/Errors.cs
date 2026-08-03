using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public record InvalidTokenError() : BadRequestError("Invalid token.");
public record OrganizationAlreadyEnabledError() : BadRequestError("Organization is already enabled.");
public record OrganizationNotPendingError() : BadRequestError("Organization is not on a Pending status.");
public record OrganizationHasKeysError() : BadRequestError("Organization already has encryption keys.");
public record EmailMismatchError() : BadRequestError("User email does not match invite.");
public record FreeOrgAdminLimitError() : BadRequestError("You can only be an admin of one free organization.");
public record SingleOrgPolicyViolationError() : BadRequestError("You cannot join this organization because you are a member of another organization which forbids it.");
public record TwoFactorRequiredError() : BadRequestError("You cannot join this organization until you enable two-step login on your user account.");
public record OrganizationUserNotFoundError() : NotFoundError("User invalid.");
public record OrganizationNotFoundError() : NotFoundError("Organization invalid.");
public record OrganizationMismatchError() : BadRequestError("User does not belong to this organization.");
