using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.Billing.Organizations;

public record PrivilegedControlsSeatLimitReachedError() : BadRequestError("Privileged Controls seat limit has been reached. Ask an Owner to add seats or raise the limit.");
public record CannotAutoscalePrivilegedControlsSeatsOnSelfHostError() : BadRequestError("Cannot autoscale Privileged Controls seats on a self-hosted instance.");
public record NotEnoughPrivilegedControlsSeatsError() : BadRequestError("Your organization has no Privileged Controls seats available. Contact sales for more seats.");
public record PrivilegedControlsSeatExpansionFailedError(string Reason) : BadRequestError(Reason);
public record PrivilegedControlsSeatReservationUnexpectedError() : InternalError("An unexpected error occurred while reserving Privileged Controls seats.");
