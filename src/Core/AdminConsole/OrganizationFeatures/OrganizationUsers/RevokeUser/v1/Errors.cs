using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;

public record CannotRevokeYourself() : BadRequestError("You cannot revoke yourself.");
public record OnlyOwnersCanRevokeOwners() : BadRequestError("Only owners can revoke other owners.");
public record CustomUsersCannotRevokeAdmins() : BadRequestError("Custom users can not revoke admins.");
public record AlreadyRevoked() : BadRequestError("Already revoked.");
public record OrgMustHaveConfirmedOwner() : BadRequestError("Organization must have at least one confirmed owner.");
