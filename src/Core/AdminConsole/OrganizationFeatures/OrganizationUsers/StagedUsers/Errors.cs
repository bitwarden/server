using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

public record OrganizationNotFound()
    : NotFoundError("Organization not found.");

public record StagedOrganizationUserNotFound()
    : NotFoundError("One or more organization members could not be found.");

public record OrganizationUserNotStaged()
    : BadRequestError("Only staged members can be sent an invitation.");

public record SeatExpansionFailed(string OrganizationName)
    : BadRequestError($"Could not add seats to {OrganizationName}. Check the organization's subscription and try again.");

public record SecretsManagerSeatExpansionFailed(string OrganizationName)
    : BadRequestError($"Could not add Secrets Manager seats to {OrganizationName}. Check the organization's subscription and try again.");
