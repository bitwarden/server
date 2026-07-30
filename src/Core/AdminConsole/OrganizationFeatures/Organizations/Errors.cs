using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public record InvalidTokenError() : BadRequestError("Invalid token.");
public record OrganizationAlreadyEnabledError() : BadRequestError("Organization is already enabled.");
public record OrganizationNotPendingError() : BadRequestError("Organization is not on a Pending status.");
public record OrganizationHasKeysError() : BadRequestError("Organization already has encryption keys.");
public record EmailMismatchError() : BadRequestError("User email does not match invite.");
public record FreeOrgAdminLimitError() : BadRequestError("You can only be an admin of 1 free organization vault.");
public record UserFreeOrgAdminLimitError() : BadRequestError("User can only be an admin of 1 free organization vault.");
public record SingleOrgPolicyViolationError() : BadRequestError("You cannot join this organization because you are a member of another organization which forbids it.");
public record TwoFactorRequiredError() : BadRequestError("You cannot join this organization until you enable two-step login on your user account.");
public record OrganizationUserNotFoundError() : NotFoundError("User invalid.");
public record OrganizationNotFoundError() : NotFoundError("Organization invalid.");
public record OrganizationMismatchError() : BadRequestError("User does not belong to this organization.");
public record PremiumLicenseError() : BadRequestError("Premium licenses cannot be applied to an organization vault. Upload this license from your personal account Settings page.");
public record LicenseAlreadyInUseError() : BadRequestError("License is already in use by another organization vault.");
public record LeaveOrgSsoBlockedError() : BadRequestError("Your organization single sign-on settings prevent you from leaving.");
public record LeaveOrgClaimedAccountError() : BadRequestError("You can't leave this organization vault because your account is claimed. Contact your admin for more information.");
public record CannotDeleteClaimedAccountError() : BadRequestError("You cannot delete accounts owned by an organization. Contact your admin for additional details.");
public record CannotPurgeClaimedAccountError() : BadRequestError("You cannot purge accounts owned by an organization. Contact your admin for additional details.");
