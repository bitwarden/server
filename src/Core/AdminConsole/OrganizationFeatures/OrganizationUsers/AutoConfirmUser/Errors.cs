using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public record OrganizationNotFound() : NotFoundError("Invalid organization");
public record FailedToWriteToEventLog() : InternalError("Failed to write to event log");
public record UserIsNotUserType() : BadRequestError("Only organization users with the User role can be automatically confirmed");
public record UserIsNotAccepted() : BadRequestError("Cannot confirm user that has not accepted the invitation.");
public record OrganizationUserIdIsInvalid() : BadRequestError("Invalid organization user id.");
public record UserDoesNotHaveTwoFactorEnabled() : BadRequestError("User does not have two-step login enabled.");
public record OrganizationEnforcesSingleOrgPolicy() : BadRequestError("Cannot confirm this member to the organization until they leave or remove all other organizations");
public record OtherOrganizationEnforcesSingleOrgPolicy() : BadRequestError("Cannot confirm this member to the organization because they are in another organization which forbids it.");
public record AutomaticallyConfirmUsersPolicyIsNotEnabled() : BadRequestError("Cannot confirm this member because the Automatically Confirm Users policy is not enabled.");
