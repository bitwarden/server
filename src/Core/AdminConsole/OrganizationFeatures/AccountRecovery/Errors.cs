using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

public record NoActionRequestedError() : BadRequestError("At least one recovery action must be requested.");
public record MissingPasswordFieldsError() : BadRequestError("Master password hash and key are required when resetting master password.");
public record FeatureDisabledError() : BadRequestError("Two-factor reset is not available.");
public record OrgDoesNotAllowResetError() : BadRequestError("Organization does not allow password reset.");
public record PolicyNotEnabledError() : BadRequestError("Organization does not have the password reset policy enabled.");
public record InvalidOrgUserError() : BadRequestError("Organization User not valid.");
public record UserNotFoundError() : NotFoundError("User not found.");
public record KeyConnectorUserError() : BadRequestError("Cannot reset password of a user with Key Connector.");
public record PasswordUpdateFailedError(string ErrorMessage) : BadRequestError(ErrorMessage);
public record OrganizationNotFoundError() : NotFoundError("Organization not found.");
