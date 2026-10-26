namespace Bit.Subscriptions.Organization.Models.Requests;

/// <summary>Relative seat change and autoscale limit for the Privileged Controls seat change. The server adds the adjustment to the organization's current seats so a stale client view never overwrites a concurrent autoscale or another owner's change.</summary>
internal record PrivilegedControlsSeatsRequest
{
    /// <summary>Seats to add (positive) or remove (negative). Null or omitted leaves the seat count unchanged, so a limit-only change never touches Stripe.</summary>
    public int? SeatAdjustment { get; init; }

    /// <summary>Null means no autoscale limit.</summary>
    public int? MaxAutoscaleSeats { get; init; }
}
