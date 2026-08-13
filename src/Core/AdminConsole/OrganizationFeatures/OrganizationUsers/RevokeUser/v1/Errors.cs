using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;

public record CannotRevokeYourself() : BadRequestError("You cannot revoke yourself.");
public record AlreadyRevoked() : BadRequestError("Already revoked.");
public record OrgMustHaveConfirmedOwner() : BadRequestError("Organization must have at least one confirmed owner.");
