using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record InviteLinkAlreadyExists()
    : ConflictError("An invite link already exists for this organization.");

public record InviteLinkDomainsRequired()
    : BadRequestError("At least one allowed domain is required.");

public record InviteLinkNotAvailable()
    : BadRequestError("Your organization's plan does not support invite links.");

public record InviteLinkNotFound()
    : NotFoundError("Invite link not found.");

public record EmailDomainNotAllowed()
    : BadRequestError("Your email domain is not allowed to join this organization.");

public record OrganizationAccessRevoked()
    : BadRequestError("Your organization access has been revoked.");

public record AlreadyOrganizationMember()
    : BadRequestError("You are already a member of this organization.");

public record ResetPasswordKeyRequired()
    : BadRequestError("Master Password reset is required, but not provided.");

public record OrganizationHasNoAvailableSeats()
    : BadRequestError("This organization has no available seats.");

public record SeatAddFailed()
    : BadRequestError("Unable to join this organization right now. Please contact your organization administrator.");

public record OnlyOneFreeOrganizationAdminAllowed()
    : BadRequestError("You can only be an admin of one free organization.");

public record ProviderUsersCannotAcceptInviteLink()
    : BadRequestError("Provider users cannot join organizations via invite link.");
