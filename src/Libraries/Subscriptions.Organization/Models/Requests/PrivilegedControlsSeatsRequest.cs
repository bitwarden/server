namespace Bit.Subscriptions.Organization.Models.Requests;

/// <summary>Absolute seat total and autoscale limit for the Privileged Controls seat change.</summary>
internal record PrivilegedControlsSeatsRequest
{
    /// <summary>Nullable so an omitted value reaches the seat rules with their own message instead of failing binding with none.</summary>
    public int? Seats { get; init; }

    /// <summary>Null means no autoscale limit.</summary>
    public int? MaxAutoscaleSeats { get; init; }
}
