using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public record OrganizationNotFound() : NotFoundError("Invalid organization");
public record FailedToWriteToEventLog() : Error("Failed to write to event log");
public record FailedToSendConfirmedUserEmail() : Error("Failed to send confirmed user email");
public record FailedToDeleteDeviceRegistration() : Error("Failed to delete device registration");
public record FailedToPushOrganizationSyncKeys() : Error("Failed to push organization sync keys");
