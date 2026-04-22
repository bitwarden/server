using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record InviteLinkAlreadyExists()
    : ConflictError("An invite link already exists for this organization.");

public record InviteLinkDomainsRequired()
    : BadRequestError("At least one allowed domain is required.");

public record InviteLinkInvalidDomains(IEnumerable<string> InvalidDomains)
    : BadRequestError($"One or more domains are invalid: {string.Join(", ", InvalidDomains)}.");

public record InviteLinkNotAvailable()
    : BadRequestError("Your organization's plan does not support invite links.");
