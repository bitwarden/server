using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums
{
    public enum GatewayType : byte
    {
        [Display(Name = "Stripe")]
        Stripe = 0,
        [Display(Name = "Braintree")]
        Braintree = 1,
        [Display(Name = "Apple App Store")]
        AppStore = 2,
        [Display(Name = "Google Play Store")]
        PlayStore = 3,
        [Display(Name = "Coinbase")]
        Coinbase = 4,
        [Display(Name = "PayPal")]
        PayPal = 1,
    }
}
