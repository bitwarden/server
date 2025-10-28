using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public record OrganizationNotFound() : NotFoundError("Invalid organization");
public record FailedToWriteToEventLog() : Error("Failed to write to event log");
public record FailedToSendConfirmedUserEmail() : Error("Failed to send confirmed user email");
public record FailedToDeleteDeviceRegistration() : Error("Failed to delete device registration");
public record FailedToPushOrganizationSyncKeys() : Error("Failed to push organization sync keys");
public record UserIsNotAccepted() : Error("Cannot confirm user that has not accepted the invitation.");
public record OrganizationUserIdIsInvalid() : Error("Invalid organization user id.");
public record UserToConfirmIsAnAdminOrOwnerOfAnotherFreeOrganization() : Error("User to confirm is an admin or owner of another free organization.");
public record UserDoesNotHaveTwoFactorEnabled() : Error("User does not have two-step login enabled.");
public record OrganizationEnforcesSingleOrgPolicy() : Error("Cannot confirm this member to the organization until they leave or remove all other organizations");
public record OtherOrganizationEnforcesSingleOrgPolicy() : Error("Cannot confirm this member to the organization because they are in another organization which forbids it.");
