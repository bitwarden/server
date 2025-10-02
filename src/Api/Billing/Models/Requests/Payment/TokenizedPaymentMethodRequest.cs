using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Payment;

public class TokenizedPaymentMethodRequest : MinimalTokenizedPaymentMethodRequest
{
    public MinimalBillingAddressRequest? BillingAddress { get; set; }

    public new (TokenizedPaymentMethod, BillingAddress?) ToDomain()
    {
        var paymentMethod = base.ToDomain();
        var billingAddress = BillingAddress?.ToDomain();
        return (paymentMethod, billingAddress);
    }
}
