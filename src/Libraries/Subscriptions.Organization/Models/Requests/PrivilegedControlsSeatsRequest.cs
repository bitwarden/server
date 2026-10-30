namespace Bit.Subscriptions.Organization.Models.Requests;

/// <summary>The number of Privileged Controls seats to add or remove, plus the autoscale limit. The server adds the adjustment to the seats it holds for the organization when the request arrives, not the count the client last saw, so a stale client view never overwrites a concurrent autoscale or another owner's change.</summary>
internal record PrivilegedControlsSeatsRequest
{
    /// <summary>Seats to add (positive) or remove (negative). Null or omitted leaves the seat count unchanged, so a limit-only change never touches Stripe.</summary>
    public int? SeatAdjustment { get; init; }

    /// <summary>Null means no autoscale limit.</summary>
    public int? MaxAutoscaleSeats { get; init; }
}
