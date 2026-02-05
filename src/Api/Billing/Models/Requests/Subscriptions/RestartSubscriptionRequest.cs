using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Models.Requests.Payment;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Subscriptions;

public class RestartSubscriptionRequest
{
    [Required]
    public required MinimalTokenizedPaymentMethodRequest PaymentMethod { get; set; }
    [Required]
    public required CheckoutBillingAddressRequest BillingAddress { get; set; }

    public (TokenizedPaymentMethod, BillingAddress) ToDomain()
        => (PaymentMethod.ToDomain(), BillingAddress.ToDomain());
}
