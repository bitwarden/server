using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

public record CannotManageTargetUser() : BadRequestError("You do not have permission to manage this user.");

public record OnlyOwnersCanManageOwners() : BadRequestError("Only an Owner can manage another Owner's account.");

public record CustomUsersCannotManageAdminsOrOwners() : BadRequestError("Custom users can not manage Admins or Owners.");

public record CustomUsersCanOnlyGrantOwnPermissions() : BadRequestError("Custom users can only grant the same custom permissions that they have.");
