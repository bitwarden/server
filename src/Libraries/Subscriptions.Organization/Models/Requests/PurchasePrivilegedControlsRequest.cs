namespace Bit.Subscriptions.Organization.Models.Requests;

internal record PurchasePrivilegedControlsRequest
{
    public required int Seats { get; init; }

    public int? MaxAutoscaleSeats { get; init; }
}
