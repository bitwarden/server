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

public record EmailDomainNotAllowed(string OrgName)
    : BadRequestError($"You're not allowed to join the {OrgName} vault with your email domain.");

public record OrganizationAccessRevoked(string OrgName)
    : BadRequestError($"Your access to the {OrgName} vault has been revoked.");
