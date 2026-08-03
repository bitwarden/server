using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;

public record TwoFactorRequiredForMembership()
    : BadRequestError("You cannot join this organization until you enable two-step login on your user account.");
