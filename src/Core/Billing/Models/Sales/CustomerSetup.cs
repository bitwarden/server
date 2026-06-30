using Bit.Core.Billing.Tax.Models;

namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class CustomerSetup
{
    public TokenizedPaymentSource? TokenizedPaymentSource { get; set; }
    public TaxInformation? TaxInformation { get; set; }

    /// <summary>
    /// Discount coupon codes provided by the checkout front end. These are subject to eligibility validation
    /// (<see cref="Bit.Core.Billing.Services.ISubscriptionDiscountService.ValidateDiscountEligibilityForUserAsync"/>)
    /// and are only applied when the target plan is <see cref="Bit.Core.Billing.Enums.ProductTierType.Families"/>.
    /// Mutually exclusive with <see cref="SystemCoupons"/>; providing both throws a <see cref="Bit.Core.Exceptions.BadRequestException"/>.
    /// </summary>
    public string[]? DiscountCoupons { get; set; }

    /// <summary>
    /// Server-managed coupon codes applied by Bitwarden itself (for example, the legacy MSP discount or
    /// the Secrets Manager standalone trial discount). These bypass user-eligibility validation and are
    /// always applied to the subscription regardless of plan type, because they are set by trusted server
    /// code rather than submitted by the end user.
    /// </summary>
    public string[]? SystemCoupons { get; set; }

    public bool IsBillable => TokenizedPaymentSource != null && TaxInformation != null;
}
