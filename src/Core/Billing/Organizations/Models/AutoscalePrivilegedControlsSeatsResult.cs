namespace Bit.Core.Billing.Organizations.Models;

/// <summary>
/// The outcome of making sure an organization has enough Privileged Controls seats for newly granted access.
/// </summary>
/// <param name="Success">
/// True when the organization has enough seats, either because it already did or because seats were added.
/// False when adding the seats would pass the organization's autoscale limit.
/// </param>
/// <param name="MaxAutoscaleSeats">The autoscale limit that blocked the change. Only set when <paramref name="Success"/> is false.</param>
public record AutoscalePrivilegedControlsSeatsResult(bool Success, int? MaxAutoscaleSeats = null);
