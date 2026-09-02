using Bit.Core.Billing.Subscriptions.Models;
using Braintree;

namespace Bit.Core.Services;

public interface IBraintreeService
{
    Task<Customer?> GetCustomer(
        Stripe.Customer customer);

    Task PayInvoice(
        SubscriberId subscriberId,
        Stripe.Invoice invoice);
}
