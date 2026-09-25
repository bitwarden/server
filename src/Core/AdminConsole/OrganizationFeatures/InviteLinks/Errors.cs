using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record InviteLinkAlreadyExists()
    : ConflictError("An invite link already exists for this organization.");

public record InviteLinkDomainsRequired()
    : BadRequestError("At least one allowed domain is required.");

public record InviteLinkNotAvailable()
    : BadRequestError("Your organization's plan does not support invite links.");

public record InviteLinkConfirmationNotSupported()
    : BadRequestError("This invite link does not support confirmation.");

public record InviteLinkNotFound()
    : NotFoundError("Invite link not found.");

public record EmailNotVerified()
    : BadRequestError("You must verify your email address before joining an organization.");

public record EmailDomainNotAllowed(string OrgName)
    : BadRequestError($"You're not allowed to join the {OrgName} vault with your email domain.");

public record OrganizationAccessRevoked(string OrgName)
    : BadRequestError($"Your access to the {OrgName} vault has been revoked.");

public record AlreadyOrganizationMember(string OrgName)
    : BadRequestError($"You're already a member of {OrgName}.");

public record ResetPasswordKeyRequired()
    : BadRequestError("Master Password reset is required, but not provided.");

public record OrganizationHasNoAvailableSeats(string OrgName)
    : BadRequestError($"The {OrgName} vault has no available seats.");

public record SeatAddFailed()
    : BadRequestError("Unable to join this vault right now. Please contact your organization admin.");

public record OnlyOneFreeOrganizationAdminAllowed()
    : BadRequestError("You can only be an admin of 1 free organization vault.");

public record ProviderUsersCannotAcceptInviteLink()
    : BadRequestError("Provider users cannot join organization vaults via invite link.");
