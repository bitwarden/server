using Stripe;

namespace Bit.Invoicing.Test;

internal static class StripeFixtures
{
    internal static Invoice SampleInvoiceWithPmSeat() => Invoice.FromJson("""
    {
      "id": "in_test", "total": 12790, "amount_due": 12790,
      "lines": { "data": [
        { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm", "metadata": { "purchasable_reference": "pm-seat" } } } } }
      ] }
    }
    """);

    internal static Subscription SampleSubscriptionWithPmSeat() => Subscription.FromJson("""
    {
      "id": "sub_test",
      "items": { "data": [
        { "quantity": 5, "price": { "id": "price_pm", "unit_amount": 2558, "metadata": { "purchasable_reference": "pm-seat" } } }
      ] }
    }
    """);
}
