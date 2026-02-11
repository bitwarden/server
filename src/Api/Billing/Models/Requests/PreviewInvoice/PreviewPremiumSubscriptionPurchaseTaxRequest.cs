using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.PreviewInvoice;

public record PreviewPremiumSubscriptionPurchaseTaxRequest
{
    [Required]
    [Range(0, 99, ErrorMessage = "Additional storage must be between 0 and 99 GB.")]
    public short AdditionalStorage { get; set; }

    [Required]
    public required MinimalBillingAddressRequest BillingAddress { get; set; }

    public string? Coupon { get; set; }

    public (short, BillingAddress, string?) ToDomain() => (AdditionalStorage, BillingAddress.ToDomain(), Coupon);
}
