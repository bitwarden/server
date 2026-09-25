using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Constants;

namespace Bit.Api.Billing.Models.Requests.Premium;

public class CreatePremiumCheckoutSessionRequest : IValidatableObject
{
    [Required]
    public required string Platform { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Platform is not (StripeConstants.CheckoutSession.Platforms.Ios
            or StripeConstants.CheckoutSession.Platforms.Android
            or StripeConstants.CheckoutSession.Platforms.Browser
            or StripeConstants.CheckoutSession.Platforms.Desktop))
        {
            yield return new ValidationResult(
                $"Platform must be '{StripeConstants.CheckoutSession.Platforms.Ios}', " +
                $"'{StripeConstants.CheckoutSession.Platforms.Android}', " +
                $"'{StripeConstants.CheckoutSession.Platforms.Browser}', " +
                $"or '{StripeConstants.CheckoutSession.Platforms.Desktop}'.",
                [nameof(Platform)]);
        }
    }
}
