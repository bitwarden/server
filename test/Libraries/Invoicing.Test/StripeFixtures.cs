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

    /// <summary>
    /// Premium ($10/yr, 1 seat) four months into the term upgrading to Families under always_invoice.
    /// Every Password Manager line is a proration, so the builder must synthesize the seat.
    /// </summary>
    internal static Invoice SampleUpgradePreviewInvoiceProrationOnly() => Invoice.FromJson("""
    {
      "id": "in_preview_premium_org_upgrade", "total": 2200, "amount_due": 2200, "starting_balance": 0,
      "total_taxes": [{ "amount": 200 }],
      "lines": { "data": [
        { "amount": -667, "quantity": 1,
          "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
          "pricing": { "price_details": { "price": { "id": "price_premium_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } },
        { "amount": 2667, "quantity": 1,
          "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
          "pricing": { "price_details": { "price": { "id": "price_families", "metadata": { "purchasable_reference": "pm-seat" } } } } }
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
