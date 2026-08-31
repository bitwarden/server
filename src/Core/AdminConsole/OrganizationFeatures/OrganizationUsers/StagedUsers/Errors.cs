using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

public record OrganizationNotFound()
    : NotFoundError("Organization not found.");

public record StagedOrganizationUserNotFound()
    : NotFoundError("One or more organization members could not be found.");

public record OrganizationUserNotStaged()
    : BadRequestError("Only staged members can be sent an invitation.");

public record SeatExpansionFailed(string Reason)
    : BadRequestError(Reason);

public record SecretsManagerSeatExpansionFailed(string Reason)
    : BadRequestError(Reason);
