using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Constants;

namespace Bit.Subscriptions.Organization.Models.Requests;

internal record PurchasePrivilegedControlsRequest
{
    private const string SeatsErrorMessage = "Privileged Controls seats must be between 1 and 100,000";

    /// <summary>Nullable so an omitted value reaches validation with its message instead of failing binding with none.</summary>
    [Required(ErrorMessage = SeatsErrorMessage)]
    [Range(1, PlanConstants.MaxPurchasableQuantity, ErrorMessage = SeatsErrorMessage)]
    public int? Seats { get; init; }

    [Range(1, PlanConstants.MaxPurchasableQuantity, ErrorMessage = "The autoscale limit must be between 1 and 100,000")]
    public int? MaxAutoscaleSeats { get; init; }
}
