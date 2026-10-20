using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Constants;

namespace Bit.Subscriptions.Organization.Models.Requests;

/// <summary>
/// Public because the minimal API validation source generator skips types it cannot access, which includes
/// internal ones.
/// </summary>
public record PurchasePrivilegedControlsRequest
{
    [Range(1, PlanConstants.MaxPurchasableQuantity, ErrorMessage = "Privileged Controls seats must be between 1 and 100,000")]
    public required int Seats { get; init; }

    [Range(1, PlanConstants.MaxPurchasableQuantity, ErrorMessage = "The autoscale limit must be between 1 and 100,000")]
    public int? MaxAutoscaleSeats { get; init; }
}
