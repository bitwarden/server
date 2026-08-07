using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;

public record DuplicateGroupId() : BadRequestError("A group id cannot be listed more than once within add or update.");
public record OverlappingGroupId() : BadRequestError("A group id cannot appear in more than one of add, update, or remove.");
public record CannotModifyDefaultUserCollectionAccess() : BadRequestError("You cannot modify group access on a collection with the type as DefaultUserCollection.");
public record GroupAlreadyHasAccess() : BadRequestError("Cannot add access for a group that already has access to this collection.");
public record GroupDoesNotHaveAccess() : BadRequestError("Cannot update access for a group that does not currently have access to this collection.");
public record GroupsNotFound() : BadRequestError("One or more groups do not exist.");
public record GroupsNotInOrganization() : BadRequestError("One or more groups do not belong to the same organization as the collection being assigned.");
public record NoRemainingManageAccess() : BadRequestError("At least one member or group must have can manage permission.");
public record InvalidManageAssociation() : BadRequestError("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
