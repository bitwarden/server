using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;

public record OrganizationUserNotFound() : NotFoundError("Organization user not found.");
public record NotEligibleForSelfRevoke() : BadRequestError("User is not eligible for self-revocation. The organization data ownership policy must be enabled and the user must be a confirmed member.");
public record LastOwnerCannotSelfRevoke() : BadRequestError("The last owner cannot revoke themselves.");
