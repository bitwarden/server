#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Attributes;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class PremiumCloudHostedSubscriptionRequest : IValidatableObject
{
    [Required]
    [PaymentMethodTypeValidation]
    public required string PaymentMethodType { get; set; }

    [Required]
    public required string PaymentToken { get; set; }

    [Range(0, 99)]
    public short AdditionalStorageGb { get; set; } = 0;

    [Required]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Country code must be 2 characters long.")]
    public required string Country { get; set; }

    public string? PostalCode { get; set; }

    public (TokenizedPaymentMethod, BillingAddress, short) ToDomain()
    {
        var paymentMethod = new TokenizedPaymentMethod
        {
            Type = TokenizablePaymentMethodTypeExtensions.From(PaymentMethodType),
            Token = PaymentToken
        };

        var billingAddress = new BillingAddress
        {
            Country = Country,
            PostalCode = PostalCode ?? string.Empty
        };

        return (paymentMethod, billingAddress, AdditionalStorageGb);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Country == "US" && string.IsNullOrWhiteSpace(PostalCode))
        {
            yield return new ValidationResult("Zip / postal code is required.", [nameof(PostalCode)]);
        }
    }
}
