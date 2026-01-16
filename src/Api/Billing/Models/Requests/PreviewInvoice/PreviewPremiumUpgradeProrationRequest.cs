using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.PreviewInvoice;

public record PreviewPremiumUpgradeProrationRequest
{
    [Required]
    public required ProductTierType TargetProductTierType { get; set; }

    [Required]
    public required MinimalBillingAddressRequest BillingAddress { get; set; }

    public (ProductTierType, BillingAddress) ToDomain() =>
        (TargetProductTierType, BillingAddress.ToDomain());
}
