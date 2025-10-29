using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;


public abstract record BadRequestError(string Message) : Error(Message);
public abstract record InternalError(string Message) : Error(Message);

public record OrganizationNotFound() : NotFoundError("Invalid organization");
public record FailedToWriteToEventLog() : InternalError("Failed to write to event log");
public record FailedToSendConfirmedUserEmail() : InternalError("Failed to send confirmed user email");
public record FailedToDeleteDeviceRegistration() : InternalError("Failed to delete device registration");
public record FailedToPushOrganizationSyncKeys() : InternalError("Failed to push organization sync keys");
public record UserIsNotAccepted() : BadRequestError("Cannot confirm user that has not accepted the invitation.");
public record OrganizationUserIdIsInvalid() : BadRequestError("Invalid organization user id.");
public record UserToConfirmIsAnAdminOrOwnerOfAnotherFreeOrganization() : BadRequestError("User to confirm is an admin or owner of another free organization.");
public record UserDoesNotHaveTwoFactorEnabled() : BadRequestError("User does not have two-step login enabled.");
public record OrganizationEnforcesSingleOrgPolicy() : BadRequestError("Cannot confirm this member to the organization until they leave or remove all other organizations");
public record OtherOrganizationEnforcesSingleOrgPolicy() : BadRequestError("Cannot confirm this member to the organization because they are in another organization which forbids it.");
public record FailedToCreateDefaultCollection() : InternalError("Failed to create default collection for user");
