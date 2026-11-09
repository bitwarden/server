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

    // Annual Enterprise renewal carrying Password Manager seats ($72/yr) and Privileged Controls seats ($12/yr).
    // total = 5 × 7200 + 5 × 1200.
    internal static Invoice SampleInvoiceWithPmAndPamSeat() => Invoice.FromJson("""
    {
      "id": "in_preview_pm_pam", "total": 42000, "amount_due": 42000, "period_end": 1789769920,
      "lines": { "data": [
        { "amount": 36000, "quantity": 5,
          "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
          "pricing": { "price_details": { "price": { "id": "price_pm_seat_annually", "unit_amount_decimal": "7200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pm-seat" } } } },
          "period": { "start": 1789769920, "end": 1821305920 } },
        { "amount": 6000, "quantity": 5,
          "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
          "pricing": { "price_details": { "price": { "id": "price_pam_seat_annually", "unit_amount_decimal": "1200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pam-seat" } } } },
          "period": { "start": 1789769920, "end": 1821305920 } }
      ] }
    }
    """);

    // The same subscription previewed with create_prorations after raising Privileged Controls seats from 5 to 10
    // with 335 days left in the term: Stripe emits an "unused time" credit and a "remaining time" charge for pam-seat,
    // plus the next-period recurring lines, pam-seat now at quantity 10. total = 36000 + 12000 - 5507 + 11014.
    internal static Invoice SampleInvoiceWithPmAndPamSeatProration() => Invoice.FromJson("""
    {
      "id": "in_preview_pm_pam_proration", "total": 53507, "amount_due": 53507, "period_end": 1821305920,
      "lines": { "data": [
        { "amount": -5507, "quantity": 5,
          "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
          "pricing": { "price_details": { "price": { "id": "price_pam_seat_annually", "unit_amount_decimal": "1200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pam-seat" } } } },
          "period": { "start": 1792361920, "end": 1821305920 } },
        { "amount": 11014, "quantity": 10,
          "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
          "pricing": { "price_details": { "price": { "id": "price_pam_seat_annually", "unit_amount_decimal": "1200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pam-seat" } } } },
          "period": { "start": 1792361920, "end": 1821305920 } },
        { "amount": 36000, "quantity": 5,
          "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
          "pricing": { "price_details": { "price": { "id": "price_pm_seat_annually", "unit_amount_decimal": "7200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pm-seat" } } } },
          "period": { "start": 1821305920, "end": 1852841920 } },
        { "amount": 12000, "quantity": 10,
          "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
          "pricing": { "price_details": { "price": { "id": "price_pam_seat_annually", "unit_amount_decimal": "1200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pam-seat" } } } },
          "period": { "start": 1821305920, "end": 1852841920 } }
      ] }
    }
    """);

    internal static Subscription SampleSubscriptionWithPmAndPamSeat() => Subscription.FromJson("""
    {
      "id": "sub_pm_pam",
      "items": { "data": [
        { "id": "si_pm", "quantity": 5, "price": { "id": "price_pm_seat_annually", "unit_amount": 7200, "unit_amount_decimal": "7200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pm-seat" } } },
        { "id": "si_pam", "quantity": 5, "price": { "id": "price_pam_seat_annually", "unit_amount": 1200, "unit_amount_decimal": "1200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pam-seat" } } }
      ] }
    }
    """);
}
