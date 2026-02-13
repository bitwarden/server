using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Models;

namespace Bit.Api.Billing.Models.Requests.PreviewInvoice;

public record PreviewPremiumSubscriptionPurchaseTaxRequest
{
    [Required]
    [Range(0, 99, ErrorMessage = "Additional storage must be between 0 and 99 GB.")]
    public short AdditionalStorage { get; set; }

    [Required]
    public required MinimalBillingAddressRequest BillingAddress { get; set; }

    [MaxLength(50)]
    public string? Coupon { get; set; }

    public (PremiumPurchasePreview, BillingAddress) ToDomain() => (
        new PremiumPurchasePreview
        {
            AdditionalStorageGb = AdditionalStorage,
            Coupon = Coupon
        },
        BillingAddress.ToDomain());
}
