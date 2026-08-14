using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

public record DuplicateOrganizationUserId() : BadRequestError("An organization user id cannot be listed more than once within add or update.");
public record OverlappingOrganizationUserId() : BadRequestError("An organization user id cannot appear in more than one of add, update, or remove.");
public record CannotModifyDefaultUserCollectionAccess() : BadRequestError("You cannot modify user access on a collection with the type as DefaultUserCollection.");
public record OrganizationUserAlreadyHasAccess() : BadRequestError("Cannot add access for a user who already has access to this collection.");
public record OrganizationUserDoesNotHaveAccess() : BadRequestError("Cannot update access for a user who does not currently have access to this collection.");
public record CannotAddSelfToCollection() : BadRequestError("You cannot add yourself to a collection.");
public record OrganizationUsersNotFound() : BadRequestError("One or more users do not exist.");
public record OrganizationUsersNotInOrganization() : BadRequestError("One or more users do not belong to the same organization as the collection being assigned.");
public record NoRemainingManageAccess() : BadRequestError("At least one member or group must have can manage permission.");
public record InvalidManageAssociation() : BadRequestError("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
