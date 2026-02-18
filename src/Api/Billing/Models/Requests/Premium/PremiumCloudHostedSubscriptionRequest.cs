using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Premium.Models;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class PremiumCloudHostedSubscriptionRequest : IValidatableObject
{
    public MinimalTokenizedPaymentMethodRequest? TokenizedPaymentMethod { get; set; }
    public NonTokenizedPaymentMethodRequest? NonTokenizedPaymentMethod { get; set; }

    [Required]
    public required MinimalBillingAddressRequest BillingAddress { get; set; }

    [Range(0, 99)]
    public short AdditionalStorageGb { get; set; } = 0;

    [MaxLength(50)]
    public string? Coupon { get; set; }

    public PremiumSubscriptionPurchase ToDomain()
    {
        // Check if TokenizedPaymentMethod or NonTokenizedPaymentMethod is provided.
        var tokenizedPaymentMethod = TokenizedPaymentMethod?.ToDomain();
        var nonTokenizedPaymentMethod = NonTokenizedPaymentMethod?.ToDomain();

        PaymentMethod paymentMethod = tokenizedPaymentMethod != null
            ? tokenizedPaymentMethod
            : nonTokenizedPaymentMethod!;

        var billingAddress = BillingAddress.ToDomain();

        return new PremiumSubscriptionPurchase
        {
            PaymentMethod = paymentMethod,
            BillingAddress = billingAddress,
            AdditionalStorageGb = AdditionalStorageGb,
            Coupon = Coupon
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TokenizedPaymentMethod == null && NonTokenizedPaymentMethod == null)
        {
            yield return new ValidationResult(
                "Either TokenizedPaymentMethod or NonTokenizedPaymentMethod must be provided.",
                new[] { nameof(TokenizedPaymentMethod), nameof(NonTokenizedPaymentMethod) }
            );
        }

        if (TokenizedPaymentMethod != null && NonTokenizedPaymentMethod != null)
        {
            yield return new ValidationResult(
                "Only one of TokenizedPaymentMethod or NonTokenizedPaymentMethod can be provided.",
                new[] { nameof(TokenizedPaymentMethod), nameof(NonTokenizedPaymentMethod) }
            );
        }
    }
}
