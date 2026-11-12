using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.Billing.Organizations.Queries;

/// <summary>
/// Checks whether an organization could reserve Privileged Controls seats without changing anything, so a caller can
/// find out before it buys Password Manager or Secrets Manager seats.
/// </summary>
public interface IValidatePrivilegedControlsSeatReservationQuery
{
    /// <summary>
    /// Applies the same seat rules as <see cref="Commands.IReservePrivilegedControlsSeatsCommand"/> without autoscaling
    /// or writing anything. Returns <c>null</c> when the seats could be reserved, otherwise the error the reservation
    /// would fail with.
    /// </summary>
    /// <param name="organization">The organization the members belong to.</param>
    /// <param name="seatsRequested">The number of members about to start using a Privileged Controls seat.</param>
    /// <param name="passwordManagerSeatsToAdd">
    /// The Password Manager seats the caller is about to buy, or zero when it buys none.
    /// </param>
    /// <remarks>
    /// Reads Stripe only to decide whether the organization is on a sales-initiated trial, and only when the Password
    /// Manager seat comparison has already failed.
    /// </remarks>
    Task<Error?> RunAsync(Organization organization, int seatsRequested, int passwordManagerSeatsToAdd);
}
