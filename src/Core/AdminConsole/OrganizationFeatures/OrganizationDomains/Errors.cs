using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public record DomainAlreadyVerifiedError() : ConflictError("Domain has already been verified.");
public record DomainNotAvailableError() : ConflictError("The domain is not available to be claimed.");
public record DuplicateDomainError() : ConflictError("A domain already exists for this organization vault.");
public record EmailNotOnVerifiedDomainError() : BadRequestError("Your account is managed by an organization, and this email address doesn't match a claimed domain.");
public record EmailClaimedByOrganizationError() : BadRequestError("This email address is claimed by an organization using Bitwarden.");
