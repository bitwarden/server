namespace Bit.Core.AdminConsole.Utilities.v2.Shared;

public record OrganizationNotFound() : NotFoundError("Organization not found");

public record OrganizationUserNotFound() : BadRequestError("Organization user not found.");
