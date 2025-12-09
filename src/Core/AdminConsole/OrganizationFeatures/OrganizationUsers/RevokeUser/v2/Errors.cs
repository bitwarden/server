using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public record UserAlreadyRevoked() : BadRequestError("Already revoked.");
public record CannotRevokeYourself() : BadRequestError("You cannot revoke yourself.");
public record OnlyOwnersCanRevokeOwners() : BadRequestError("Only owners can revoke other owners.");
public record MustHaveConfirmedOwner() : BadRequestError("Organization must have at least one confirmed owner.");
