using Bit.Core.Billing.Subscriptions.Models;
using Stripe;

namespace Bit.Core.Services;

public interface IBraintreeService
{
    Task PayInvoice(
        SubscriberId subscriberId,
        Invoice invoice);
}
