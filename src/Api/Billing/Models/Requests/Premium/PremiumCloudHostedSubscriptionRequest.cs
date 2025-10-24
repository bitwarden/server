using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class PremiumCloudHostedSubscriptionRequest : IValidatableObject
{
    public MinimalTokenizedPaymentMethodRequest? TokenizedPaymentMethod { get; set; }
    public NonTokenizedPaymentMethodRequest? NonTokenizedPaymentMethod { get; set; }

    [Required]
    public required MinimalBillingAddressRequest BillingAddress { get; set; }

    [Range(0, 99)]
    public short AdditionalStorageGb { get; set; } = 0;


    public (PaymentMethod, BillingAddress, short) ToDomain()
    {
        // Check if TokenizedPaymentMethod or NonTokenizedPaymentMethod is provided.
        var tokenizedPaymentMethod = TokenizedPaymentMethod?.ToDomain();
        var nonTokenizedPaymentMethod = NonTokenizedPaymentMethod?.ToDomain();

        PaymentMethod paymentMethod = tokenizedPaymentMethod != null
            ? tokenizedPaymentMethod
            : nonTokenizedPaymentMethod!;

        var billingAddress = BillingAddress.ToDomain();

        return (paymentMethod, billingAddress, AdditionalStorageGb);
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
