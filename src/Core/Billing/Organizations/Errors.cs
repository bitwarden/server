using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.Billing.Organizations;

public record OrganizationAlreadyUsesSecretsManagerError() : BadRequestError("Organization already uses Secrets Manager.");
public record OrganizationPlanDoesNotSupportSecretsManagerError() : BadRequestError("Organization's plan does not support Secrets Manager.");
public record SecretsManagerPaymentMethodNotFoundError() : ConflictError("No payment method found.");
public record SecretsManagerSubscriptionNotFoundError() : ConflictError("No subscription found.");
public record CannotAddSecretsManagerWithNegativeSeatsError() : BadRequestError("You cannot add Secrets Manager with a negative number of seats.");
public record AtLeastOneSecretsManagerSeatRequiredError() : BadRequestError("At least one Secrets Manager seat is required.");
public record CannotAddSecretsManagerWithNegativeMachineAccountsError() : BadRequestError("You cannot add Secrets Manager with a negative number of Machine Accounts.");
