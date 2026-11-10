using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.Billing.Organizations.Models;

/// <summary>
/// The outcome of reserving Privileged Controls seats for members who are about to be given access.
/// </summary>
/// <param name="Error">
/// Why the seats could not be reserved: <see cref="PrivilegedControlsSeatLimitReachedError"/>,
/// <see cref="CannotAutoscalePrivilegedControlsSeatsOnSelfHostError"/>,
/// <see cref="PrivilegedControlsSeatExpansionFailedError"/>, or
/// <see cref="PrivilegedControlsSeatReservationUnexpectedError"/>. Null when the reservation succeeded.
/// </param>
/// <param name="Exception">The exception that caused the error, when one was thrown.</param>
public sealed record PrivilegedControlsSeatReservationResult(Error? Error = null, Exception? Exception = null)
{
    public bool Success => Error is null;
}
