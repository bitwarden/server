using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record InviteLinkAlreadyExists()
    : ConflictError("An invite link already exists for this organization.");

public record InviteLinkDomainsRequired()
    : BadRequestError("At least one allowed domain is required.");

public record InviteLinkEncryptedKeyRequired()
    : BadRequestError("An encrypted invite key is required.");
