using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v1;

public record CannotRevokeYourselfError() : BadRequestError("You cannot revoke yourself.");
public record OnlyOwnersCanRevokeOwnersError() : BadRequestError("Only owners can revoke other owners.");
public record CustomUsersCannotRevokeAdminsError() : BadRequestError("Custom users can not revoke admins.");
public record AlreadyRevokedError() : BadRequestError("Already revoked.");
public record OrgMustHaveConfirmedOwnerError() : BadRequestError("Organization must have at least one confirmed owner.");
