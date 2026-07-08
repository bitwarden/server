using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

// Use generic "Resource not found." messages to avoid enumeration.
public record CollectionNotFound() : NotFoundError("Resource not found.");
public record GroupNotFound() : NotFoundError("Resource not found.");

public record InviteUserFirst() : BadRequestError("Invite the user first.");
public record CannotBeAdminOfMultipleFreeOrgs() : BadRequestError("User can only be an admin of one free organization.");
public record CannotAddSelfToCollection() : BadRequestError("You cannot add yourself to a collection.");
public record MustHaveConfirmedOwner() : BadRequestError("Organization must have at least one confirmed owner.");
public record OnlyOwnersCanManageOwners() : BadRequestError("Only an Owner can manage another Owner's account.");
public record CustomUsersCannotManageAdminsOrOwners() : BadRequestError("Custom users can not manage Admins or Owners.");
public record CustomUsersCanOnlyGrantOwnPermissions() : BadRequestError("Custom users can only grant the same custom permissions that they have.");
public record ManageMutuallyExclusive() : BadRequestError("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
public record CustomPermissionsNotEnabled() : BadRequestError("To enable custom permissions the organization must be on an Enterprise plan.");
public record CannotAssignDefaultCollection() : BadRequestError("Default collections cannot be assigned to a member.");
public record CannotAutoscaleSecretsManagerSeatsOnSelfHost() : BadRequestError("Cannot autoscale on a self-hosted instance.");
